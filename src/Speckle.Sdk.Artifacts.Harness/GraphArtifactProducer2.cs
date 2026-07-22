using System.Collections;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Speckle.DoubleNumerics;
using Speckle.Objects.Data;
using Speckle.Objects.Geometry;
using Speckle.Objects.Other;
using Speckle.Objects.Utils;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;
using Speckle.Sdk.Models.GraphTraversal;
using Speckle.Sdk.Models.Instances;
using Speckle.Sdk.Models.Proxies;

namespace Speckle.Sdk.Artifacts.Harness;

/// <summary>
/// Migrates a stored Speckle <see cref="Base"/> graph into the artefact bundle.
///
/// Discovery runs off <see cref="DefaultTraversal"/>, which yields exactly the two node kinds we migrate —
/// every <see cref="Collection"/> and every ATOMIC object — depth-first, parent before child. Traversal only
/// descends the <c>elements</c> lineage, so it halts at an atomic object: its <c>displayValue</c> and data
/// properties are handled here, never walked into.
/// </summary>
internal sealed class GraphArtifactProducer2(ObjectsArtifactPipeline pipeline, ILogger<GraphArtifactProducer2> logger)
  : IDisposable
{
  public void Dispose() => pipeline?.Dispose();

  private readonly Stats _stats = new();

  private readonly Dictionary<string, int> _collectionMap = new(StringComparer.Ordinal); // collection appId → node K
  private readonly Dictionary<string, int> _objectKMap = new(StringComparer.Ordinal); // object id → object K, for SUBELEMENT hosts

  // Traverse re-yields shared subtrees; dedup emission by content id.
  private readonly HashSet<string> _visited = new(StringComparer.Ordinal);

  // appIds actually emitted — lets proxy refs to absent targets be skipped rather than minting phantom Ks.
  private readonly HashSet<string> _seenObjectAppIds = new(StringComparer.Ordinal);
  private readonly HashSet<string> _seenGeometryAppIds = new(StringComparer.Ordinal);

  // INSTANCE-node K by appId, shared between atomic instance leaves and nested-instance definition members.
  private readonly Dictionary<string, int> _instanceNodeByAppId = new(StringComparer.Ordinal);

  // object/geometry appId → its appearance targets ("g:<geomAppId>" mesh | "o:<objAppId>" instance object),
  // so an object-grained material/colour proxy can reach the object's display geometry.
  private readonly Dictionary<string, List<string>> _objectDisplayGeomKeys = new(StringComparer.Ordinal);

  // geometry appId → material embedded on the mesh (old-style sends with no root proxies).
  private readonly Dictionary<string, RenderMaterial> _embeddedMaterialByGeom = new(StringComparer.Ordinal);

  // definition-member appId → its raw-solid geometry appId ("<appId>:solid"), so the DEFINES pass can link the
  // lossless solid alongside the member's display meshes (mirrors the Rhino connector's memberSolidK).
  private readonly Dictionary<string, string> _defMemberSolidKey = new(StringComparer.Ordinal);

  // v2 backwards-compat: true when the root is not a Collection (an older property-tree commit). In this mode the
  // collection hierarchy is synthesized from each atomic object's dynamic-property path (see EmitV2HierarchyEdge).
  private bool _isv2PreCollections;

  // synthetic collection K by cumulative property path ("Level 1/Walls") — dedup + parent-chain building (v2 mode).
  private readonly Dictionary<string, int> _v2CollByPath = new(StringComparer.Ordinal);

  /// <summary>Walks <paramref name="root"/>, drives the pipeline, and <see cref="ObjectsArtifactPipeline.Complete"/>s
  /// it. Returns the run stats.</summary>
  public Stats Produce(Base root)
  {
    // A Collection-rooted commit (the ~90% case) drives the true collection path. An older v2 commit is a plain
    // Base property tree with no collections — its hierarchy is synthesized from dynamic-property names instead.
    _isv2PreCollections = root is not Collection;

    // Definition CONTENT (rendered via instance placements) that also appears in the tree — routed to
    // geometry/instance nodes instead of being emitted as scene objects.
    var defSourceAppIds = GetDefinitionAppIds(root);

    var traversal = DefaultTraversal.CreateTraversalFunc();
    foreach (var tc in traversal.Traverse(root))
    {
      var current = tc.Current;

      // Only the root has no parent; it's a container, never emitted (its child collections come back top-level).
      if (tc.Parent is null)
      {
        continue;
      }
      if (!_visited.Add(current.id.NotNull()))
      {
        continue;
      }
      // A raw-Base root/wrapper is descended fully, so its proxy arrays surface here — never scene nodes.
      // InstanceProxy is not one of these: a bare placement IS an atomic leaf.
      if (IsValueOrDefinitionProxy(current))
      {
        continue;
      }

      if (current is Collection col)
      {
        var parentK = NearestCollectionK(tc);
        var k = pipeline.AddCollection(CollectionKey(col), col.name, parentK, CollectionSubtype(col));
        _collectionMap[Aid(col)] = k;
        _stats.Collections++;
        continue;
      }

      if (defSourceAppIds.Contains(Aid(current)))
      {
        EmitDefinitionMember(current);
        continue;
      }

      var objK = EmitObject(current);
      _objectKMap[current.id.NotNull()] = objK;
      if (_isv2PreCollections)
      {
        EmitV2HierarchyEdge(tc, objK);
      }
      else
      {
        EmitHierarchyEdge(tc, objK);
      }
    }

    var layerGeomKeys = BuildLayerGeomKeys(root, out var layerDepth);

    EmitProxies(root, layerGeomKeys, layerDepth);

    _stats.Geometries = _seenGeometryAppIds.Count;
    pipeline.Complete();
    return _stats;
  }

  // ── hierarchy edges ─────────────────────────────────────────────────────────────────

  // Nearest emitted collection above the node (skipping non-collection wrappers). The root collection is
  // excluded, so its child collections come back top-level.
  private int? NearestCollectionK(TraversalContext tc)
  {
    for (var p = tc.Parent; p is not null; p = p.Parent)
    {
      if (p.Current is Collection pc)
      {
        return p.Parent is null || !_collectionMap.TryGetValue(Aid(pc), out var k) ? null : k;
      }
    }
    return null;
  }

  // Every atomic object belongs to its nearest ancestor collection (IN_COLLECTION), regardless of what sits
  // between them. If an object host (e.g. a DataObject carrying `elements`) is one of those in-between nodes,
  // it also gets a host→hosted SUBELEMENT to the nearest such host.
  private void EmitHierarchyEdge(TraversalContext tc, int objK)
  {
    var hostFound = false;
    for (var p = tc.Parent; p is not null; p = p.Parent)
    {
      var pc = p.Current;
      if (pc is Collection)
      {
        if (_collectionMap.TryGetValue(Aid(pc), out var ck))
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

  // v2 backwards-compat: with no Collection nodes, synthesize a collection per dynamic-property-path segment
  // (mirrors the connectors' GetCollectionPath). A real Collection ancestor still wins if one exists.
  private void EmitV2HierarchyEdge(TraversalContext tc, int objK)
  {
    // SUBELEMENT host walk (as EmitHierarchyEdge). No-ops in a pure v2 tree; kept for typed-host parents.
    var hostFound = false;
    for (var p = tc.Parent; p is not null; p = p.Parent)
    {
      if (!hostFound && _objectKMap.TryGetValue(p.Current.id.NotNull(), out var hostK))
      {
        pipeline.Subelement(hostK, objK, _stats.SubelementEdges++);
        hostFound = true;
      }
    }

    int? leafK = NearestCollectionK(tc); // a real Collection ancestor, if any; otherwise synthesize from the path
    if (leafK is null)
    {
      int? parentK = null;
      var path = "";
      foreach (var name in tc.GetPropertyPath().Reverse()) // child→root, so reverse to build root→leaf
      {
        path = path.Length == 0 ? name : path + "/" + name;
        if (!_v2CollByPath.TryGetValue(path, out var pathK))
        {
          pathK = pipeline.AddCollection("v2:" + path, CleanName(name), parentK, "Layer");
          _v2CollByPath[path] = pathK;
          _stats.Collections++;
        }
        parentK = pathK;
      }
      leafK = parentK; // null when the object sits directly on the root (no property path) → stays top-level
    }

    if (leafK is int ck)
    {
      pipeline.InCollection(objK, ck, 0);
      _stats.InCollectionEdges++;
    }
  }

  // v2 detached dynamic props are often "@"-prefixed (e.g. "@Walls"); the synthesized collection name drops it.
  private static string CleanName(string name) => name.StartsWith('@') ? name[1..] : name;

  // ── object emission ─────────────────────────────────────────────────────────────────

  private int EmitObject(Base obj)
  {
    var appId = Aid(obj);
    var objK = pipeline.InternObject(appId);
    if (!_seenObjectAppIds.Add(appId))
    {
      return objK; // shared reference, same K
    }

    _stats.Objects++;

    var (props, rootScalars, typeKey) = ExtractProperties(obj);
    pipeline.AddProperties(appId, props, rootScalars, typeKey);

    if (obj is InstanceProxy ip)
    {
      var instK = ResolveInstanceNode(ip);
      pipeline.DisplayInstance(objK, instK, 0);
      _stats.DisplayInstanceEdges++;
      _stats.InstanceAtomics++;
      // ByBlock: the placed definition geometry is shared, so appearance rides on the instance object.
      RecordObjectGeom(appId, "o:" + appId);
      return objK;
    }

    // Lossless raw solid (Brep/Extrusion/SubD/SolidX, or a Rhino/Autocad host wrapper): link the native blob via
    // the SOLID rel, in ADDITION to the display meshes below. Receive picks solid vs mesh via PreferSolids.
    var rawEnc = TryReadRawEncoding(obj);
    if (rawEnc is not null && IsMigratableSolidFormat(rawEnc.format) && EmitSolidBlob(appId, rawEnc) is int solidK)
    {
      pipeline.Solid(objK, solidK, 0);
      _stats.Solids++;
      // Deliberately NOT recorded in _objectDisplayGeomKeys — materials/colours bind to display meshes, not the solid.
    }

    // Checked before the raw-geometry case so a leaf that ships a display mesh (Brep/SubD, extrusions) encodes
    // that mesh rather than its un-encodable self.
    var displayValue = GetBaseList(obj, "displayValue").ToList();
    if (displayValue.Count > 0)
    {
      // Element-level material fills gaps where a mesh carries none of its own.
      var objMaterial = ReadEmbeddedMaterial(obj);
      int ord = 0;
      foreach (var item in displayValue)
      {
        if (item is InstanceProxy dip)
        {
          var dInstK = ResolveInstanceNode(dip);
          pipeline.DisplayInstance(objK, dInstK, ord++);
          _stats.DisplayInstanceEdges++;
        }
        else
        {
          var gAppId = Aid(item);
          if (AddGeometry(gAppId, item))
          {
            pipeline.Display(objK, pipeline.InternGeometryId(gAppId), ord++);
            _stats.DisplayEdges++;
            RecordObjectGeom(appId, "g:" + gAppId);
            if ((ReadEmbeddedMaterial(item) ?? objMaterial) is { } rm)
            {
              _embeddedMaterialByGeom.TryAdd(gAppId, rm);
            }
          }
        }
      }
      return objK;
    }

    // A display-less raw-encoded object (e.g. a BrepX with an empty displayValue) has no SGEO encoding; its solid
    // blob is already captured above, so don't fall through and feed it to SgeoEncoder (which would throw).
    if (obj is IRawEncodedObject)
    {
      return objK;
    }

    if (IsGeometry(obj))
    {
      // The leaf is its own geometry; appId interns into both the object and geometry namespaces.
      if (AddGeometry(appId, obj))
      {
        pipeline.Display(objK, pipeline.InternGeometryId(appId), 0);
        _stats.DisplayEdges++;
        _stats.MeshAtomics++;
        RecordObjectGeom(appId, "g:" + appId);
        if (ReadEmbeddedMaterial(obj) is { } rm)
        {
          _embeddedMaterialByGeom.TryAdd(appId, rm);
        }
      }
    }

    return objK;
  }

  private void RecordObjectGeom(string objAppId, string geomAppId)
  {
    if (!_objectDisplayGeomKeys.TryGetValue(objAppId, out var list))
    {
      list = new List<string>();
      _objectDisplayGeomKeys[objAppId] = list;
    }
    list.Add(geomAppId);
  }

  // Idempotent by appId; also emits the placement's DEFINITION so DEFINES_INSTANCE can resolve.
  private int ResolveInstanceNode(InstanceProxy ip)
  {
    var key = Aid(ip);
    if (_instanceNodeByAppId.TryGetValue(key, out var existing))
    {
      return existing;
    }
    var defK = pipeline.AddDefinition(ip.definitionId, ip.definitionId);
    var instK = pipeline.AddInstance(key, defK, Flatten(ip.transform), ip.units);
    _instanceNodeByAppId[key] = instK;
    return instK;
  }

  private bool AddGeometry(string appId, Base geometry)
  {
    pipeline.AddGeometry(appId, geometry);
    _seenGeometryAppIds.Add(appId);
    return true;
  }

  // Definition content: a nested instance → INSTANCE node (linked via DEFINES_INSTANCE); otherwise a geometry
  // blob under the member's appId (linked via DEFINES), preferring a display mesh over an un-encodable parent.
  private void EmitDefinitionMember(Base obj)
  {
    var appId = Aid(obj);

    if (obj is InstanceProxy ip)
    {
      if (!_instanceNodeByAppId.ContainsKey(appId))
      {
        ResolveInstanceNode(ip);
        _stats.DefinitionInstances++;
      }
      return;
    }

    if (!_seenGeometryAppIds.Add(appId))
    {
      return;
    }

    // Lossless raw solid for a definition member rides DEFINES (no standalone SOLID edge) alongside its display
    // meshes — the EmitProxies DEFINES pass links it via _defMemberSolidKey (mirrors the Rhino connector).
    var rawEnc = TryReadRawEncoding(obj);
    var hasSolid =
      rawEnc is not null && IsMigratableSolidFormat(rawEnc.format) && EmitSolidBlob(appId, rawEnc) is not null;
    if (hasSolid)
    {
      _defMemberSolidKey[appId] = appId + ":solid";
      _stats.DefinitionSolids++;
    }

    // Don't SGEO-encode a raw-encoded object itself (BrepX/…) — only a genuine display mesh or plain geometry leaf.
    var geometry =
      GetBaseList(obj, "displayValue").FirstOrDefault(d => d is not InstanceProxy)
      ?? (obj is not IRawEncodedObject && IsGeometry(obj) ? obj : null);
    if (geometry is null)
    {
      _seenGeometryAppIds.Remove(appId); // no SGEO mesh under appId; a solid-only member is linked via _defMemberSolidKey
      if (!hasSolid)
      {
        _stats.SkippedDefines++;
        if (_stats.Notes.Count < 20)
        {
          _stats.Notes.Add($"def member {appId} has no encodable geometry [{obj.speckle_type}]");
        }
      }
      return;
    }

    pipeline.AddGeometry(appId, geometry);
    _stats.DefinitionGeometries++;
    if ((ReadEmbeddedMaterial(geometry) ?? ReadEmbeddedMaterial(obj)) is { } rm)
    {
      _embeddedMaterialByGeom.TryAdd(appId, rm);
    }
    // Shared across placements, so ByLayer colour must bind to this geometry-K, not flood from an instance.
    _objectDisplayGeomKeys[appId] = new List<string> { "g:" + appId };
  }

  // ── proxy / value-node emission ─────────────────────────────────────────────────────

  private void EmitProxies(
    Base root,
    Dictionary<string, List<string>> layerGeomKeys,
    Dictionary<string, int> layerDepth
  )
  {
    // A proxy ref resolved to its tagged appearance targets ("g:" mesh | "o:" instance object).
    IReadOnlyList<string> DirectGeomKeys(string refAppId)
    {
      if (_objectDisplayGeomKeys.TryGetValue(refAppId, out var og))
      {
        return og;
      }
      if (_seenGeometryAppIds.Contains(refAppId))
      {
        return new[] { "g:" + refAppId };
      }
      return Array.Empty<string>();
    }

    // Binds value-nodes to geometry. A direct (mesh/object) ref claims a geometry; a Layer ref then fills only
    // what's unclaimed, and among nested layers the deepest wins (else a parent layer floods its sub-layers).
    Dictionary<string, int> BindWithPrecedence(List<(int nodeK, List<string> refs)> proxies, out int skipped)
    {
      var byGeom = new Dictionary<string, int>(StringComparer.Ordinal);
      var skip = 0;
      foreach (var (nodeK, refs) in proxies)
      {
        foreach (var r in refs)
        {
          var direct = DirectGeomKeys(r);
          if (direct.Count == 0 && !layerGeomKeys.ContainsKey(r))
          {
            skip++;
          }
          foreach (var gk in direct)
          {
            byGeom.TryAdd(gk, nodeK);
          }
        }
      }
      var layerCandidate = new Dictionary<string, (int depth, int nodeK)>(StringComparer.Ordinal);
      foreach (var (nodeK, refs) in proxies)
      {
        foreach (var r in refs)
        {
          if (!layerGeomKeys.TryGetValue(r, out var lg))
          {
            continue;
          }
          var depth = layerDepth.GetValueOrDefault(r, 0);
          foreach (var gk in lg)
          {
            if (byGeom.ContainsKey(gk))
            {
              continue; // a direct ref already claimed it
            }
            if (!layerCandidate.TryGetValue(gk, out var cur) || depth > cur.depth)
            {
              layerCandidate[gk] = (depth, nodeK);
            }
          }
        }
      }
      foreach (var (gk, (_, nodeK)) in layerCandidate)
      {
        byGeom[gk] = nodeK;
      }
      skipped = skip;
      return byGeom;
    }

    foreach (var def in GetBaseList(root, "instanceDefinitionProxies"))
    {
      if (def is not InstanceDefinitionProxy idp)
      {
        continue;
      }
      var defK = pipeline.AddDefinition(DefinitionKey(idp), idp.name);
      _stats.Definitions++;
      int o = 0;
      foreach (var memberAppId in idp.objects)
      {
        if (_instanceNodeByAppId.TryGetValue(memberAppId, out var instK))
        {
          pipeline.DefinesInstance(defK, instK, o++);
          _stats.DefinesInstanceEdges++;
        }
        else
        {
          // Solid and display mesh both DEFINES under the same member ordinal (solid first) so receive can
          // group them and prefer the solid.
          var any = false;
          if (_defMemberSolidKey.TryGetValue(memberAppId, out var solidKey))
          {
            pipeline.Defines(defK, pipeline.InternGeometryId(solidKey), o);
            _stats.DefinesEdges++;
            any = true;
          }
          if (_seenGeometryAppIds.Contains(memberAppId))
          {
            pipeline.Defines(defK, pipeline.InternGeometryId(memberAppId), o);
            _stats.DefinesEdges++;
            any = true;
          }
          if (any)
          {
            o++;
          }
          else
          {
            _stats.SkippedDefines++;
          }
        }
      }
    }

    var matProxies = new List<(int, List<string>)>();
    var placeholderMatKs = new HashSet<int>();
    foreach (var mat in GetBaseList(root, "renderMaterialProxies"))
    {
      if (mat is not RenderMaterialProxy rmp)
      {
        continue;
      }
      var v = rmp.value;
      var matK = pipeline.AddMaterial(MaterialKey(rmp), v.diffuse, v.opacity, v.metalness, v.roughness);
      matProxies.Add((matK, rmp.objects));
      _stats.Materials++;
      // Pure-black diffuse is the CAD "no material / ByLayer" placeholder; it must yield to a real colour.
      if ((v.diffuse & 0xFFFFFF) == 0)
      {
        placeholderMatKs.Add(matK);
      }
    }
    var matBindings = BindWithPrecedence(matProxies, out var matSkipped);
    _stats.SkippedMaterial += matSkipped;

    // Fallback for sends with no root material proxies: bind each mesh's embedded material directly (a proxy
    // claim wins). Deduped by material identity. An embedded black is explicit, not the CAD placeholder.
    var embeddedMatKs = new Dictionary<string, int>(StringComparer.Ordinal);
    foreach (var (gAppId, rm) in _embeddedMaterialByGeom)
    {
      var gk = "g:" + gAppId;
      if (matBindings.ContainsKey(gk))
      {
        continue;
      }
      var key = rm.applicationId ?? "mat:" + (rm.id ?? rm.diffuse.ToString(CultureInfo.InvariantCulture));
      if (!embeddedMatKs.TryGetValue(key, out var matK))
      {
        matK = pipeline.AddMaterial(key, rm.diffuse, rm.opacity, rm.metalness, rm.roughness);
        embeddedMatKs[key] = matK;
        _stats.Materials++;
      }
      matBindings[gk] = matK;
    }

    var colProxies = new List<(int, List<string>)>();
    foreach (var col in GetBaseList(root, "colorProxies"))
    {
      if (col is not ColorProxy cp)
      {
        continue;
      }
      colProxies.Add((pipeline.AddColor(cp.value), cp.objects));
      _stats.Colors++;
    }
    var colBindings = BindWithPrecedence(colProxies, out var colSkipped);
    _stats.SkippedColor += colSkipped;

    foreach (var (target, matK) in matBindings)
    {
      if (target[0] != 'g')
      {
        continue; // instances carry no per-mesh material
      }
      // Placeholder black yields to a real display colour on the same mesh.
      if (placeholderMatKs.Contains(matK) && colBindings.ContainsKey(target))
      {
        continue;
      }
      pipeline.HasMaterial(pipeline.InternGeometryId(target[2..]), matK);
      _stats.HasMaterialEdges++;
    }
    // Geometry-targeted only: an object-target would collide with a geometry-target at the same numeric K.
    foreach (var (target, colK) in colBindings)
    {
      if (target[0] == 'o')
      {
        continue;
      }
      pipeline.HasColor(pipeline.InternGeometryId(target[2..]), colK);
      _stats.HasColorEdges++;
    }

    // No strong-typed LevelProxy in the SDK, so read generically. Refs to elements absent from the export are skipped.
    foreach (var lvl in GetBaseList(root, "levelProxies"))
    {
      var (name, elevation) = ReadLevelValue(lvl);
      var lvlK = pipeline.AddLevel(LevelKey(lvl, name), name, elevation);
      _stats.Levels++;
      foreach (var objAppId in AsStringList(lvl["objects"]))
      {
        if (!_seenObjectAppIds.Contains(objAppId))
        {
          _stats.SkippedLevel++;
          continue;
        }
        pipeline.OnLevel(pipeline.InternObject(objAppId), lvlK);
        _stats.OnLevelEdges++;
      }
    }
  }

  // ── layer geometry resolution (ByLayer material/colour) ─────────────────────────────

  // collection appId → the display geometry of every object beneath it, with each collection's depth — so a
  // ByLayer proxy (which references a layer) can bind to that layer's meshes.
  private Dictionary<string, List<string>> BuildLayerGeomKeys(Base root, out Dictionary<string, int> layerDepth)
  {
    var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
    var depths = new Dictionary<string, int>(StringComparer.Ordinal);
    void Walk(Base node, int depth)
    {
      foreach (var child in GetBaseList(node, "elements"))
      {
        if (child is not Collection)
        {
          continue;
        }
        var geoms = new List<string>();
        CollectDescendantGeom(child, geoms);
        if (geoms.Count > 0)
        {
          result[Aid(child)] = geoms;
          depths[Aid(child)] = depth;
        }
        Walk(child, depth + 1);
      }
    }
    Walk(root, 0);
    layerDepth = depths;
    return result;
  }

  private void CollectDescendantGeom(Base collection, List<string> acc)
  {
    foreach (var child in GetBaseList(collection, "elements"))
    {
      if (child is Collection)
      {
        CollectDescendantGeom(child, acc);
      }
      else if (_objectDisplayGeomKeys.TryGetValue(Aid(child), out var gks))
      {
        acc.AddRange(gks);
      }
    }
  }

  // ── property extraction ─────────────────────────────────────────────────────────────

  private static (
    IReadOnlyDictionary<string, object?> props,
    IEnumerable<KeyValuePair<string, object?>> rootScalars,
    string? typeKey
  ) ExtractProperties(Base obj)
  {
    IReadOnlyDictionary<string, object?> props = obj is DataObject dobj
      ? dobj.properties
      : obj.GetMembers(DynamicBaseMemberType.Instance | DynamicBaseMemberType.Dynamic);

    // `level` is the level NAME and lives at the top level (not under properties), so it must be listed here.
    var rootScalars = new List<KeyValuePair<string, object?>>
    {
      new("speckle_type", obj.speckle_type),
      new("name", obj["name"]),
      new("units", obj["units"]),
      new("category", obj["category"]),
      new("family", obj["family"]),
      new("type", obj["type"]),
      new("level", obj["level"]),
    };

    var typeKey = obj["typeId"] as string ?? (props.TryGetValue("typeId", out var tk) ? tk as string : null);

    return (props, rootScalars, typeKey);
  }

  private static (string? name, double elevation) ReadLevelValue(Base lvl)
  {
    if (lvl["value"] is Base valueBase)
    {
      return (valueBase["name"] as string, ToDouble(valueBase["elevation"]));
    }
    if (lvl["value"] is IReadOnlyDictionary<string, object?> vd)
    {
      return (vd.GetValueOrDefault("name") as string, ToDouble(vd.GetValueOrDefault("elevation")));
    }
    return (lvl["name"] as string, ToDouble(lvl["elevation"]));
  }

  // ── keys (applicationId-keyed; null → stable per-object key) ─────────────────────────

  private static string Aid(Base b) => b.applicationId ?? "spk:" + b.id;

  private static string CollectionKey(Collection col) => col.applicationId ?? "coll:" + col.id;

  private static string DefinitionKey(InstanceDefinitionProxy idp) => idp.applicationId ?? idp.name;

  private static string MaterialKey(RenderMaterialProxy rmp) =>
    rmp.applicationId ?? rmp.value.applicationId ?? "mat:" + rmp.value.diffuse.ToString(CultureInfo.InvariantCulture);

  private static string LevelKey(Base lvl, string? name) => lvl.applicationId ?? "lvl:" + (name ?? lvl.id);

  private static string CollectionSubtype(Collection col)
  {
#pragma warning disable CS0618 // Type or member is obsolete
    var ct = col.collectionType;
#pragma warning restore CS0618 // Type or member is obsolete
    return string.IsNullOrEmpty(ct) ? col.speckle_type.Split('.')[^1] : ct;
  }

  // ── helpers ─────────────────────────────────────────────────────────────────────────

  private HashSet<string> GetDefinitionAppIds(Base root)
  {
    var set = new HashSet<string>(StringComparer.Ordinal);
    foreach (var def in GetBaseList(root, "instanceDefinitionProxies"))
    {
      if (def is not InstanceDefinitionProxy idp)
      {
        logger.LogWarning(
          "Encountered a {ActualType} where expected a {ExpectedType}",
          def.GetType(),
          typeof(InstanceDefinitionProxy)
        );
        continue;
      }
      foreach (var appId in idp.objects)
      {
        set.Add(appId);
      }
    }
    return set;
  }

  // InstanceProxy is deliberately excluded — a bare placement can be an atomic leaf.
  private static bool IsValueOrDefinitionProxy(Base b) =>
    b is InstanceDefinitionProxy or RenderMaterialProxy or ColorProxy or GroupProxy;

  private static bool IsGeometry(Base b) => b.speckle_type.StartsWith("Objects.Geometry.", StringComparison.Ordinal);

  private static RenderMaterial? ReadEmbeddedMaterial(Base host) =>
    (host["renderMaterial"] ?? host["@renderMaterial"]) as RenderMaterial;

  // ── raw-encoded solids (SOLID rel) ───────────────────────────────────────────────────

  // 3dm (Rhino) and sat (Autocad) are the native solid formats we migrate; others (e.g. dwg) are skipped.
  private static bool IsMigratableSolidFormat(string? format) =>
    format is RawEncodingFormats.RHINO_3DM or RawEncodingFormats.ACAD_SAT;

  // Reads the lossless raw encoding off a raw-encoded geometry (encodedValue) or a host wrapper (rawEncoding).
  // Typed casts only — the v3 graph deserializes into these registered SDK types.
  private static RawEncoding? TryReadRawEncoding(Base obj) =>
    obj switch
    {
      IRawEncodedObject r => r.encodedValue,
      RhinoObject ro => ro.rawEncoding,
      AutocadObject ao => ao.rawEncoding,
      _ => null,
    };

  // base64-decodes the blob and stores it under "<objAppId>:solid" as a raw (non-SGEO) geometry, returning its K.
  // A malformed blob is logged and skipped (null) rather than failing the whole migration run.
  private int? EmitSolidBlob(string objAppId, RawEncoding enc)
  {
    byte[] bytes;
    try
    {
      bytes = Convert.FromBase64String(enc.contents);
    }
    catch (FormatException ex)
    {
      logger.LogWarning(ex, "Skipped malformed raw {Format} encoding on {AppId}", enc.format, objAppId);
      if (_stats.Notes.Count < 20)
      {
        _stats.Notes.Add($"malformed raw {enc.format} encoding on {objAppId}");
      }
      return null;
    }
    return pipeline.AddRawGeometry(objAppId + ":solid", bytes, enc.format);
  }

  // A detached list may sit under the typed key or the `@`-prefixed dynamic key; take the first non-empty one.
  private static IEnumerable<Base> GetBaseList(Base b, string key)
  {
    var raw = NonEmpty(b[key]) ?? NonEmpty(b["@" + key]);
    if (raw is IEnumerable seq and not string)
    {
      foreach (var item in seq)
      {
        if (item is Base bs)
        {
          yield return bs;
        }
      }
    }
  }

  private static object? NonEmpty(object? v) => v is ICollection c && c.Count == 0 ? null : v;

  private static IEnumerable<string> AsStringList(object? raw)
  {
    if (raw is IEnumerable seq and not string)
    {
      foreach (var item in seq)
      {
        if (item is string s)
        {
          yield return s;
        }
      }
    }
  }

  private static double ToDouble(object? o) =>
    o switch
    {
      double d => d,
      float f => f,
      int i => i,
      long l => l,
      _ => 0.0,
    };

  private static double[] Flatten(Matrix4x4 m) =>
    new[]
    {
      m.M11,
      m.M12,
      m.M13,
      m.M14,
      m.M21,
      m.M22,
      m.M23,
      m.M24,
      m.M31,
      m.M32,
      m.M33,
      m.M34,
      m.M41,
      m.M42,
      m.M43,
      m.M44,
    };

  // ── stats ───────────────────────────────────────────────────────────────────────────

  internal sealed class Stats
  {
    public int Objects;
    public int Geometries;
    public int DisplayEdges;
    public int DisplayInstanceEdges;
    public int SubelementEdges;
    public int Definitions;
    public int DefinesEdges;
    public int Materials;
    public int HasMaterialEdges;
    public int Colors;
    public int HasColorEdges;
    public int Levels;
    public int OnLevelEdges;
    public int Collections;
    public int InCollectionEdges;
    public int DefinitionGeometries;
    public int DefinitionInstances;
    public int DefinesInstanceEdges;
    public int MeshAtomics;
    public int InstanceAtomics;

    // Raw (non-SGEO) native solid blobs: SOLID edges on atomic objects, DEFINES-linked solids on def members.
    public int Solids;
    public int DefinitionSolids;

    // Proxy refs whose target appId isn't in the graph — skipped rather than minting a phantom K.
    public int SkippedDefines;
    public int SkippedMaterial;
    public int SkippedColor;
    public int SkippedLevel;
    public int SkippedDangling => SkippedDefines + SkippedMaterial + SkippedColor + SkippedLevel;
    public readonly List<string> Notes = new();

    public override string ToString() =>
      $"""
        objects={Objects} (meshAtomic={MeshAtomics} instAtomic={InstanceAtomics})  geometries={Geometries} (defGeom={DefinitionGeometries})
        edges: DISPLAY={DisplayEdges} DISPLAY_INSTANCE={DisplayInstanceEdges} SUBELEMENT={SubelementEdges} SOLID={Solids} (defSolid={DefinitionSolids})
               DEFINES={DefinesEdges} DEFINES_INSTANCE={DefinesInstanceEdges} HAS_MATERIAL={HasMaterialEdges} HAS_COLOR={HasColorEdges} ON_LEVEL={OnLevelEdges} IN_COLLECTION={InCollectionEdges}
        nodes: DEFINITION={Definitions} INSTANCE(def)={DefinitionInstances} MATERIAL={Materials} COLOR={Colors} LEVEL={Levels} COLLECTION={Collections}
        skipped (ref not in graph): {SkippedDangling}  (DEFINES={SkippedDefines} HAS_MATERIAL={SkippedMaterial} HAS_COLOR={SkippedColor} ON_LEVEL={SkippedLevel})
        """;
  }
}
