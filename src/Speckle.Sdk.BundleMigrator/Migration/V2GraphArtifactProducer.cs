using System.Globalization;
using Speckle.Objects.Deprecated;
using Speckle.Objects.Geometry;
using Speckle.Objects.Other;
using Speckle.Objects.Utils;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;
using Speckle.Sdk.Models.GraphTraversal;
using Speckle.Sdk.Pipelines;
using Speckle.Sdk.Pipelines.Send.Artifacts;

namespace Speckle.Sdk.BundleMigrator.Migration;

/// <summary>
/// Migrates a legacy (v2) <see cref="Base"/> graph into the artefact bundle — one with no root-level proxies.
///
/// Everything proxy-driven is absent by definition, so this path carries only the v2 workarounds:
/// <list type="bullet">
///   <item>materials are embedded on the object or its meshes rather than published as proxies;</item>
///   <item>pre-2.13 sends have no <see cref="Collection"/> objects at all, so the scene tree is synthesized
///   from each object's dynamic-property path (2.13+ sends do have real Collections and use them);</item>
///   <item>a <see cref="RenderMaterial"/> can turn up in the <c>elements</c> lineage where an atomic object
///   is expected.</item>
/// </list>
/// </summary>
internal sealed class V2GraphArtifactProducer(ObjectsArtifactPipeline pipeline, ArtifactHelper helper)
  : IGraphArtifactProducer
{
  public void Dispose() => pipeline?.Dispose();

  private readonly Stats _stats = new();

  private readonly Dictionary<string, int> _collectionMap = new(StringComparer.Ordinal); // collection appId → node K
  private readonly Dictionary<string, int> _objectKMap = new(StringComparer.Ordinal); // object id → object K, for SUBELEMENT hosts

  private readonly HashSet<string> _seenObjectAppIds = new(StringComparer.Ordinal);
  private readonly HashSet<string> _seenGeometryAppIds = new(StringComparer.Ordinal);

  // geometry appId → material embedded on the mesh. The only source of materials on a v2 graph.
  private readonly Dictionary<string, RenderMaterial> _embeddedMaterialByGeom = new(StringComparer.Ordinal);

  // distinct argb seen — AddColor interns, so this only keeps _stats.Colors honest.
  private readonly HashSet<int> _seenColorArgb = new();

  // synthetic collection K by cumulative property path ("Level 1/Walls") — dedup + parent-chain building.
  private readonly Dictionary<string, int> _v2CollByPath = new(StringComparer.Ordinal);

  // LEVEL node K by level identity — v2 attached a whole Level object to every element on it.
  private readonly Dictionary<string, int> _levelKByKey = new(StringComparer.Ordinal);

  // pre v2.13 commits did not use Collection objects, so the scene tree has to be synthesized instead.
  private bool _preCollections;

  public Stats Produce(Base root)
  {
    _preCollections = root is not Collection;

    var traversal = DefaultTraversal.CreateTraversalFunc();
    foreach (var tc in traversal.Traverse(root))
    {
      Base current = tc.Current;
      TraversalContext? parent = tc.Parent;

      if (parent is null)
      {
        continue;
      }

      if (current is RenderMaterial)
      {
        // Traversal should be preventing RenderMaterials from being returned, but since not all v2 commits are aligned
        // with traversal, this covers cases where RenderMaterial is yielded anyway
        continue;
      }

      if (TryEmitLegacyCameraView(current))
      {
        continue; // a viewpoint, not a scene object
      }

      if (current is Collection col)
      {
        // Parent is a Collection except at the root, which is skipped above and never mapped → top-level (null).
        int? parentK = _collectionMap.TryGetValue(helper.Aid(parent.Current), out var pk) ? pk : null;
        var k = pipeline.AddCollection(helper.CollectionKey(col), col.name, parentK, helper.CollectionSubtype(col));
        _collectionMap[helper.Aid(col)] = k;
        _stats.Collections++;
        continue;
      }

      var objK = EmitObject(current);
      _objectKMap[current.id.NotNull()] = objK;
      if (_preCollections)
      {
        EmitSynthesizedHierarchyEdge(tc, objK);
      }
      else
      {
        EmitHierarchyEdge(tc, objK);
      }
    }

    EmitEmbeddedMaterials();
    EmitSceneView();

    _stats.Geometries = _seenGeometryAppIds.Count;
    pipeline.Complete();
    return _stats;
  }

  private void EmitHierarchyEdge(TraversalContext tc, int objK)
  {
    var hostFound = false;
    for (var p = tc.Parent; p is not null; p = p.Parent)
    {
      var pc = p.Current;
      if (pc is Collection)
      {
        if (_collectionMap.TryGetValue(helper.Aid(pc), out var ck))
        {
          pipeline.InCollection(objK, ck, 0);
          _stats.InCollectionEdges++;
        }
        return; // nearest collection reached; membership resolved
      }
      if (!hostFound && _objectKMap.TryGetValue(pc.id.NotNull(), out var hostK))
      {
        pipeline.Subelement(hostK, objK, _stats.SubelementEdges++);
        hostFound = true;
      }
      // keep walking up to reach the enclosing collection
    }
  }

  //  Pre ~2.13 + Grasshopper versions cotnain no Collections, instead dynamic props were used for structure.
  private void EmitSynthesizedHierarchyEdge(TraversalContext tc, int objK)
  {
    if (tc.Parent is not null && _objectKMap.TryGetValue(tc.Parent.Current.id.NotNull(), out var hostK))
    {
      pipeline.Subelement(hostK, objK, _stats.SubelementEdges++);
    }

    int? parentK = null;
    var path = "";
    foreach (var name in tc.GetPropertyPath().Reverse()) // child→root, so reverse to build root→leaf
    {
      path = path.Length == 0 ? name : path + "/" + name;
      if (!_v2CollByPath.TryGetValue(path, out var pathK))
      {
        pathK = pipeline.AddCollection(path, name.TrimStart('@'), parentK, "Layer");
        _v2CollByPath[path] = pathK;
        _stats.Collections++;
      }
      parentK = pathK;
    }
    var leafK = parentK; // null when the object sits directly on the root (no property path) → stays top-level

    if (leafK is not { } ck)
    {
      throw new InvalidOperationException("Object has no collection");
    }

    pipeline.InCollection(objK, ck, 0);
    _stats.InCollectionEdges++;
  }

  private void EmitSceneView()
  {
    List<SceneViewKey> keys = _stats.InCollectionEdges > 0 ? [SceneViewKey.Rel(RelKind.InCollection)] : [];
    pipeline.AddDefaultSceneView(keys);
  }

  private int EmitObject(Base obj)
  {
    var appId = helper.Aid(obj);
    var objK = pipeline.InternObject(appId);
    if (!_seenObjectAppIds.Add(appId))
    {
      return objK; // shared reference, same K
    }

    _stats.Objects++;

    var (props, rootScalars, typeKey) = helper.ExtractProperties(obj);
    pipeline.AddProperties(appId, props, rootScalars, typeKey);

    // Checked before the raw-geometry case so a leaf that ships a display mesh encodes that mesh rather than
    // its un-encodable self.
    var displayValue = helper.GetBaseList(obj, "displayValue").ToList();
    if (displayValue.Count > 0)
    {
      // Element-level material fills gaps where a mesh carries none of its own.
      var objMaterial = helper.ReadEmbeddedMaterial(obj);
      int ord = 0;
      foreach (var item in displayValue)
      {
        var gAppId = helper.Aid(item);
        if (AddGeometry(gAppId, item))
        {
          pipeline.Display(objK, pipeline.InternGeometryId(gAppId), ord++);
          _stats.DisplayEdges++;
          if ((helper.ReadEmbeddedMaterial(item) ?? objMaterial) is { } rm)
          {
            _embeddedMaterialByGeom.TryAdd(gAppId, rm);
          }
          EmitDisplayStyleColor(item, gAppId);
        }
      }

      // A displayValue is the best signal for a buildElement, which is what v2 attached levels to.
      EmitLevelEdge(obj, objK);
      return objK;
    }

    if (helper.IsGeometry(obj))
    {
      // The leaf is its own geometry; appId interns into both the object and geometry namespaces.
      if (AddGeometry(appId, obj))
      {
        pipeline.Display(objK, pipeline.InternGeometryId(appId), 0);
        _stats.DisplayEdges++;
        _stats.MeshAtomics++;
        if (helper.ReadEmbeddedMaterial(obj) is { } rm)
        {
          _embeddedMaterialByGeom.TryAdd(appId, rm);
        }
        EmitDisplayStyleColor(obj, appId);
      }
    }

    return objK;
  }

  private bool AddGeometry(string appId, Base geometry)
  {
    var k = pipeline.AddGeometryMigrated(appId, geometry);
    if (k is null)
    {
      return false; // Surface/Vector skipped — no geometry row was written, so emit no edge.
    }
    _seenGeometryAppIds.Add(appId);
    return true;
  }

  // ── levels ──────────────────────────────────────────────────────────────────────────

  // v2 had no level proxies: the Level object hung off each element's `level` property, repeated per element.
  // Best-effort — a level with no usable elevation is skipped rather than migrated at a made-up height.
  private void EmitLevelEdge(Base obj, int objK)
  {
    if (helper.ReadV2Level(obj) is not { } lvl || helper.ReadDouble(lvl, "elevation") is not { } elevation)
    {
      return;
    }

    var name = lvl["name"] as string;
    var key = lvl.id ?? lvl.applicationId ?? $"{name}:{elevation}"; // id is the reliable v2 level identity
    if (!_levelKByKey.TryGetValue(key, out var lvlK))
    {
      lvlK = pipeline.AddLevel(key, name, elevation);
      _levelKByKey[key] = lvlK;
      _stats.Levels++;
    }

    pipeline.OnLevel(objK, lvlK);
    _stats.OnLevelEdges++;
  }

  // ── camera views ────────────────────────────────────────────────────────────────────

  // v2 never recorded a lens; the legacy Rhino baker assumed a fixed 50mm perspective, so match it.
  // Fov is that lens on a 35mm frame: 2*atan((24/2) / 50).
  private const double LegacyLensMm = 50.0;
  private static readonly double LegacyFovDegrees = 2.0 * Math.Atan(12.0 / LegacyLensMm) * (180.0 / Math.PI);

  private int _cameraViewOrd;

  /// <summary>True when <paramref name="obj"/> was a view, so the caller stops treating it as a scene
  /// object. A v2 view lands in the <see cref="LegacyV2"/> catch-all, so its members are only readable
  /// dynamically.</summary>
  private bool TryEmitLegacyCameraView(Base obj)
  {
    // LegacyV2 is the sink for a dozen legacy types, so the full member set is the only way to spot a view.
    // `name` and `target` are excluded — both are legitimately null on a valid one.
    if (
      obj is not LegacyV2
      || obj["origin"] is not Point origin
      || obj["forwardDirection"] is not Vector forward
      || obj["upDirection"] is not Vector up
      || obj["isOrthogonal"] is not bool isOrtho
    )
    {
      return false;
    }

    var name = obj["name"] as string;
    if (!helper.TryNormalize(forward) || !helper.TryNormalize(up))
    {
      _stats.Notes.Add($"view '{name}' skipped: degenerate forward/up vector");
      return true;
    }

    var target = obj["target"] as Point;
    var ord = _cameraViewOrd++;

    pipeline.AddCameraView(
      new CameraView(
        View: ord,
        Name: name,
        IsDefault: false,
        Ord: ord,
        PosX: origin.x,
        PosY: origin.y,
        PosZ: origin.z,
        ForwardX: forward.x,
        ForwardY: forward.y,
        ForwardZ: forward.z,
        UpX: up.x,
        UpY: up.y,
        UpZ: up.z,
        TargetX: target?.x,
        TargetY: target?.y,
        TargetZ: target?.z,
        Units: obj["units"] as string ?? origin.units,
        IsOrtho: isOrtho,
        // Both must be null for an ortho view.
        Fov: isOrtho ? null : LegacyFovDegrees,
        LensMm: isOrtho ? null : LegacyLensMm
      )
    );
    _stats.CameraViews++;
    return true;
  }

  // v2 attached a DisplayStyle to the geometry itself; its colour is the closest v4 equivalent (HAS_COLOR).
  // linetype / lineweight / name have no v4 equivalent and are dropped. An absent colour emits nothing —
  // the deleted class's LightGray field default cannot apply to the LegacyV2 the style now deserializes into.
  private void EmitDisplayStyleColor(Base geometry, string geomAppId)
  {
    if (helper.ReadV2DisplayStyle(geometry) is not { } style || helper.ReadArgb(style, "color") is not { } argb)
    {
      return;
    }

    var colK = pipeline.AddColor(argb); // interned by argb, so repeats are free
    if (_seenColorArgb.Add(argb))
    {
      _stats.Colors++;
    }
    pipeline.HasColor(pipeline.InternGeometryId(geomAppId), colK);
    _stats.HasColorEdges++;
  }

  // Mints one MATERIAL node per distinct embedded material and binds it to the meshes carrying it. Unlike the
  // proxy path there is no placeholder rule — an embedded black is an explicit assignment, not the CAD sentinel.
  private void EmitEmbeddedMaterials()
  {
    var materialKs = new Dictionary<string, int>(StringComparer.Ordinal);
    foreach (var (gAppId, rm) in _embeddedMaterialByGeom)
    {
      var key = rm.applicationId ?? "mat:" + (rm.id ?? rm.diffuse.ToString(CultureInfo.InvariantCulture));
      if (!materialKs.TryGetValue(key, out var matK))
      {
        // emissive is passed naively — the pipeline normalizes a black RGB to the bundle's NULL "no emission".
        matK = pipeline.AddMaterial(
          key,
          rm.name,
          rm.diffuse,
          rm.opacity,
          rm.metalness,
          rm.roughness,
          rm.emissive,
          helper.ReadDouble(rm, "ior") // untyped on RenderMaterial
        );
        materialKs[key] = matK;
        _stats.Materials++;
      }
      pipeline.HasMaterial(pipeline.InternGeometryId(gAppId), matK);
      _stats.HasMaterialEdges++;
    }
  }
}
