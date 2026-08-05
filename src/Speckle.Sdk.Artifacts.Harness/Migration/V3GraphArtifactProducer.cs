using Speckle.Objects.Geometry;
using Speckle.Objects.Other;
using Speckle.Objects.Utils;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;
using Speckle.Sdk.Models.GraphTraversal;
using Speckle.Sdk.Models.Instances;
using Speckle.Sdk.Models.Proxies;
using Speckle.Sdk.Pipelines;
using Speckle.Sdk.Pipelines.Send.Artifacts;

namespace Speckle.Sdk.Artifacts.Harness.Migration;

/// <summary>
/// Migrates a v3 <see cref="Base"/> graph — one carrying root-level proxies — into the artefact bundle.
///
/// Discovery runs off <see cref="DefaultTraversal"/>, which yields exactly the two node kinds we migrate —
/// every <see cref="Collection"/> and every ATOMIC object — depth-first, parent before child. Traversal only
/// descends the <c>elements</c> lineage, so it halts at an atomic object: its <c>displayValue</c> and data
/// properties are handled here, never walked into.
///
/// Appearance (material/colour) is proxy-driven: a proxy naming a mesh or object claims it directly, and a
/// proxy naming a layer fills whatever is left, deepest layer first.
/// </summary>
internal sealed class V3GraphArtifactProducer(ObjectsArtifactPipeline pipeline, ArtifactHelper helper)
  : IGraphArtifactProducer
{
  public void Dispose() => pipeline?.Dispose();

  private readonly Stats _stats = new();

  private readonly Dictionary<string, int> _collectionMap = new(StringComparer.Ordinal); // collection appId → node K
  private readonly Dictionary<string, int> _objectKMap = new(StringComparer.Ordinal); // object id → object K, for SUBELEMENT hosts

  // appIds actually emitted — lets proxy refs to absent targets be skipped rather than minting phantom Ks.
  private readonly HashSet<string> _seenObjectAppIds = new(StringComparer.Ordinal);
  private readonly HashSet<string> _seenGeometryAppIds = new(StringComparer.Ordinal);

  // Revit room/host refs stashed during traversal, resolved after it when _seenObjectAppIds is complete.
  private readonly record struct TopologyRefs(
    string ElementAppId,
    string? Room,
    string? Space,
    string? FromRoom,
    string? ToRoom,
    string? Parent
  );

  private readonly List<TopologyRefs> _topologyRefs = new();

  // objKs that already received a lineage SUBELEMENT — the host-derived pass must not duplicate them.
  private readonly HashSet<int> _lineageSubelementChildKs = new();

  // INSTANCE-node K by appId, shared between atomic instance leaves and nested-instance definition members.
  private readonly Dictionary<string, int> _instanceNodeByAppId = new(StringComparer.Ordinal);

  // object/geometry appId → its appearance targets ("g:<geomAppId>" mesh | "o:<objAppId>" instance object)
  private readonly Dictionary<string, List<string>> _objectDisplayGeomKeys = new(StringComparer.Ordinal);

  // definition-member appId → its raw-solid geometry appId ("<appId>:solid")
  private readonly Dictionary<string, string> _defMemberSolidKey = new(StringComparer.Ordinal);

  public Stats Produce(Base root)
  {
    IReadOnlySet<string> defSourceAppIds = GetDefinitionAppIds(root);

    var traversal = DefaultTraversal.CreateTraversalFunc();
    foreach (var tc in traversal.Traverse(root))
    {
      Base current = tc.Current;
      TraversalContext? parent = tc.Parent;

      if (parent is null)
      {
        continue;
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

      if (defSourceAppIds.Contains(helper.Aid(current)))
      {
        EmitDefinitionMember(current);
        continue;
      }

      var objK = EmitObject(current);
      _objectKMap[current.id.NotNull()] = objK;
      EmitHierarchyEdge(tc, objK);
    }

    var layerGeomKeys = BuildLayerGeomKeys(root, out var layerDepth);

    EmitProxies(root, layerGeomKeys, layerDepth);
    EmitRevitTopology();
    EmitCameraViews(root);
    EmitSceneView();

    _stats.Geometries = _seenGeometryAppIds.Count;
    pipeline.Complete();
    return _stats;
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
        _lineageSubelementChildKs.Add(objK);
        hostFound = true;
      }
      // keep walking up to reach the enclosing collection
    }
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
    StashTopologyRefs(appId, props);

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
    var rawEnc = helper.TryReadRawEncoding(obj);
    if (
      rawEnc is not null
      && helper.IsMigratableSolidFormat(rawEnc.format)
      && EmitSolidBlob(appId, rawEnc) is int solidK
    )
    {
      pipeline.Solid(objK, solidK, 0);
      _stats.Solids++;
      // Deliberately NOT recorded in _objectDisplayGeomKeys — materials/colours bind to display meshes, not the solid.
    }
    // Checked before the raw-geometry case so a leaf that ships a display mesh (Brep/SubD, extrusions) encodes
    // that mesh rather than its un-encodable self.
    var displayValue = helper.GetBaseList(obj, "displayValue").ToList();
    if (displayValue.Count > 0)
    {
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
          var gAppId = helper.Aid(item);
          if (AddGeometry(gAppId, item))
          {
            pipeline.Display(objK, pipeline.InternGeometryId(gAppId), ord++);
            _stats.DisplayEdges++;
            RecordObjectGeom(appId, "g:" + gAppId);
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

    if (helper.IsGeometry(obj))
    {
      // The leaf is its own geometry; appId interns into both the object and geometry namespaces.
      if (AddGeometry(appId, obj))
      {
        pipeline.Display(objK, pipeline.InternGeometryId(appId), 0);
        _stats.DisplayEdges++;
        _stats.MeshAtomics++;
        RecordObjectGeom(appId, "g:" + appId);
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
    var key = helper.Aid(ip);
    if (_instanceNodeByAppId.TryGetValue(key, out var existing))
    {
      return existing;
    }
    var defK = pipeline.AddDefinition(ip.definitionId, ip.definitionId);
    var instK = pipeline.AddInstance(key, defK, helper.Flatten(ip.transform), ip.units);
    _instanceNodeByAppId[key] = instK;
    return instK;
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

  // Definition content: a nested instance → INSTANCE node (linked via DEFINES_INSTANCE); otherwise a geometry
  // blob under the member's appId (linked via DEFINES), preferring a display mesh over an un-encodable parent.
  private void EmitDefinitionMember(Base obj)
  {
    var appId = helper.Aid(obj);

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
    var rawEnc = helper.TryReadRawEncoding(obj);
    var hasSolid =
      rawEnc is not null && helper.IsMigratableSolidFormat(rawEnc.format) && EmitSolidBlob(appId, rawEnc) is not null;
    if (hasSolid)
    {
      _defMemberSolidKey[appId] = appId + ":solid";
      _stats.DefinitionSolids++;
    }

    // Don't SGEO-encode a raw-encoded object itself (BrepX/…) — only a genuine display mesh or plain geometry leaf.
    var geometry =
      helper.GetBaseList(obj, "displayValue").FirstOrDefault(d => d is not InstanceProxy)
      ?? (obj is not IRawEncodedObject && helper.IsGeometry(obj) ? obj : null);
    if (geometry is null)
    {
      _seenGeometryAppIds.Remove(appId); // no SGEO mesh under appId; a solid-only member is linked via _defMemberSolidKey
      if (!hasSolid)
      {
        _stats.SkippedDefines++;
        _stats.Notes.Add($"def member {appId} has no encodable geometry [{obj.speckle_type}]");
      }
      return;
    }

    if (pipeline.AddGeometryMigrated(appId, geometry) is null)
    {
      return;
    }
    _stats.DefinitionGeometries++;
    // Shared across placements, so ByLayer colour must bind to this geometry-K, not flood from an instance.
    _objectDisplayGeomKeys[appId] = new List<string> { "g:" + appId };
  }

  // base64-decodes the blob and stores it under "<objAppId>:solid" as a raw (non-SGEO) geometry, returning its K.
  private int? EmitSolidBlob(string objAppId, RawEncoding enc)
  {
    byte[] bytes;
    try
    {
      bytes = Convert.FromBase64String(enc.contents);
      return pipeline.AddRawGeometry(objAppId + ":solid", bytes, enc.format);
    }
    catch (FormatException ex)
    {
      throw new InvalidOperationException($"Malformed solid {objAppId}", ex);
    }
  }

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

    foreach (var def in helper.GetBaseList(root, "instanceDefinitionProxies"))
    {
      if (def is not InstanceDefinitionProxy idp)
      {
        continue;
      }
      var defK = pipeline.AddDefinition(helper.DefinitionKey(idp), idp.name);
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
    foreach (var mat in helper.GetBaseList(root, "renderMaterialProxies"))
    {
      if (mat is not RenderMaterialProxy rmp)
      {
        continue;
      }
      var v = rmp.value;
      // emissive is passed naively — the pipeline normalizes a black RGB to the bundle's NULL "no emission".
      var matK = pipeline.AddMaterial(
        helper.MaterialKey(rmp),
        v.name,
        v.diffuse,
        v.opacity,
        v.metalness,
        v.roughness,
        v.emissive,
        helper.ReadDouble(v, "ior") // untyped on RenderMaterial
      );
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

    var colProxies = new List<(int, List<string>)>();
    foreach (var col in helper.GetBaseList(root, "colorProxies"))
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

    // Refs to elements absent from the export are skipped.
    foreach (var lvl in helper.GetBaseList(root, "levelProxies"))
    {
      if (lvl is not LevelProxy lp)
      {
        continue;
      }
      var name = lp.value.name;
      var elevation = helper.ReadDouble(lp.value, "elevation") ?? 0.0; // dynamic member on the level DataObject
      var lvlK = pipeline.AddLevel(helper.LevelKey(lp, name), name, elevation);
      _stats.Levels++;
      foreach (var objAppId in lp.objects)
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

    // v3 records no group nesting, so every group is top-level — an outer group already lists its inner
    // groups' members.
    foreach (var grp in helper.GetBaseList(root, "groupProxies"))
    {
      if (grp is not GroupProxy gp)
      {
        continue;
      }

      // Resolved up front so an all-dangling group leaves no edgeless CONTAINER behind.ons.
      var members = new List<int>();
      var seen = new HashSet<string>(StringComparer.Ordinal);
      foreach (var objAppId in gp.objects)
      {
        if (!seen.Add(objAppId))
        {
          continue; // unpack can visit a block sub-object twice
        }
        if (!_seenObjectAppIds.Contains(objAppId))
        {
          _stats.SkippedGroup++;
          continue;
        }
        members.Add(pipeline.InternObject(objAppId));
      }

      if (members.Count == 0)
      {
        continue;
      }

      var grpK = pipeline.AddContainer(helper.GroupKey(gp), gp.name, null, "Group");
      _stats.Groups++;
      foreach (var objK in members)
      {
        pipeline.InGroup(objK, grpK, 0);
        _stats.InGroupEdges++;
      }
    }
  }

  // Revit v3 recorded room/host topology as flat property keys holding bare Element.UniqueIds
  // (ClassPropertiesExtractor, FamilyInstance only). Stashed here, resolved post-traversal.
  private void StashTopologyRefs(string appId, IReadOnlyDictionary<string, object?> props)
  {
    string? Str(string key) => props.TryGetValue(key, out var v) && v is string { Length: > 0 } s ? s : null;

    var room = Str("roomApplicationId");
    var space = Str("spaceApplicationId");
    var fromRoom = Str("fromRoomApplicationId");
    var toRoom = Str("toRoomApplicationId");
    var parent = Str("parentApplicationId");
    if (room is null && space is null && fromRoom is null && toRoom is null && parent is null)
    {
      return;
    }
    _topologyRefs.Add(new TopologyRefs(appId, room, space, fromRoom, toRoom, parent));
  }

  // Mirrors big-truck's EmitElementTopology: room ?? space → IN_ROOM, from/to rooms → one CONNECTS_TO scoped
  // by the opening's K, host parent → SUBELEMENT unless the lineage already provided one.
  private void EmitRevitTopology()
  {
    foreach (var t in _topologyRefs)
    {
      var elementK = pipeline.InternObject(t.ElementAppId);

      if (t.Parent is not null && !_lineageSubelementChildKs.Contains(elementK))
      {
        if (TryResolveRef(t.Parent, t.ElementAppId, out var parent) && parent != t.ElementAppId)
        {
          pipeline.Subelement(pipeline.InternObject(parent), elementK, 0);
          _stats.HostSubelementEdges++;
        }
        else
        {
          _stats.SkippedHostParent++;
        }
      }

      var roomRef = t.Room ?? t.Space;
      if (roomRef is not null)
      {
        if (TryResolveRef(roomRef, t.ElementAppId, out var room))
        {
          pipeline.InRoom(elementK, pipeline.InternObject(room), 0);
          _stats.InRoomEdges++;
        }
        else
        {
          _stats.SkippedRoom++;
        }
      }

      if (t.FromRoom is not null && t.ToRoom is not null)
      {
        if (
          TryResolveRef(t.FromRoom, t.ElementAppId, out var from) && TryResolveRef(t.ToRoom, t.ElementAppId, out var to)
        )
        {
          pipeline.ConnectsTo(pipeline.InternObject(from), pipeline.InternObject(to), elementK);
          _stats.ConnectsToEdges++;
        }
        else
        {
          _stats.SkippedConnects++;
        }
      }
    }
  }

  // Matches the v3 connector's linked-model appId suffix: {UniqueId}_t{8 lowercase hex}
  // (LinkedModelHandler.GetTransformHash).
  private static bool TryGetTransformSuffix(string appId, out string suffix)
  {
    suffix = "";
    if (appId.Length <= 10 || appId[^10] != '_' || appId[^9] != 't')
    {
      return false;
    }
    for (var i = appId.Length - 8; i < appId.Length; i++)
    {
      if (appId[i] is not ((>= '0' and <= '9') or (>= 'a' and <= 'f')))
      {
        return false;
      }
    }
    suffix = appId[^10..];
    return true;
  }

  // Bare refs resolve exact first, else within the referrer's transform scope — the connector wrote refs as
  // bare UniqueIds while suffixing sent appIds, and referrer + target share a document, hence a suffix.
  private bool TryResolveRef(string bareRef, string referrerAppId, out string resolved)
  {
    if (_seenObjectAppIds.Contains(bareRef))
    {
      resolved = bareRef;
      return true;
    }
    if (TryGetTransformSuffix(referrerAppId, out var suffix) && _seenObjectAppIds.Contains(bareRef + suffix))
    {
      resolved = bareRef + suffix;
      return true;
    }
    resolved = "";
    return false;
  }

  // collection appId → the display geometry of every object beneath it, with each collection's depth — so a
  // ByLayer proxy (which references a layer) can bind to that layer's meshes.
  private Dictionary<string, List<string>> BuildLayerGeomKeys(Base root, out Dictionary<string, int> layerDepth)
  {
    var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
    var depths = new Dictionary<string, int>(StringComparer.Ordinal);
    void Walk(Base node, int depth)
    {
      foreach (var child in helper.GetBaseList(node, "elements"))
      {
        if (child is not Collection)
        {
          continue;
        }
        var geoms = new List<string>();
        CollectDescendantGeom(child, geoms);
        if (geoms.Count > 0)
        {
          result[helper.Aid(child)] = geoms;
          depths[helper.Aid(child)] = depth;
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
    foreach (var child in helper.GetBaseList(collection, "elements"))
    {
      if (child is Collection)
      {
        CollectDescendantGeom(child, acc);
      }
      else if (_objectDisplayGeomKeys.TryGetValue(helper.Aid(child), out var gks))
      {
        acc.AddRange(gks);
      }
    }
  }

  // Root-level viewpoints; the traversal only descends `elements`, so they're read directly.
  // A v3 Camera has no target/fov/lens/ortho data, so those columns stay null.
  private void EmitCameraViews(Base root)
  {
    var ord = 0;
    foreach (var key in (string[])["views", "cameras"])
    {
      foreach (var view in helper.GetBaseList(root, key))
      {
        if (view is not Camera cam)
        {
          continue;
        }
        // Nothing downstream validates unit length, so normalize here and drop what we can't orient.
        if (!helper.TryNormalize(cam.forward) || !helper.TryNormalize(cam.up))
        {
          _stats.Notes.Add($"camera '{cam.name}' skipped: degenerate forward/up vector");
          continue;
        }

        pipeline.AddCameraView(
          new CameraView(
            View: ord,
            Name: cam.name,
            IsDefault: false,
            Ord: ord,
            PosX: cam.position.x,
            PosY: cam.position.y,
            PosZ: cam.position.z,
            ForwardX: cam.forward.x,
            ForwardY: cam.forward.y,
            ForwardZ: cam.forward.z,
            UpX: cam.up.x,
            UpY: cam.up.y,
            UpZ: cam.up.z,
            Units: cam.position.units
          )
        );
        _stats.CameraViews++;
        ord++;
      }
    }
  }

  private void EmitSceneView()
  {
    var keys = new List<SceneViewKey>();
    if (_stats.InCollectionEdges > 0)
    {
      keys.Add(SceneViewKey.Rel(RelKind.InCollection));
      if (_stats.OnLevelEdges > 0)
      {
        keys.Add(SceneViewKey.Rel(RelKind.OnLevel));
      }
    }
    pipeline.AddDefaultSceneView(keys);
  }

  private HashSet<string> GetDefinitionAppIds(Base root)
  {
    var set = new HashSet<string>(StringComparer.Ordinal);
    foreach (var def in helper.GetBaseList(root, "instanceDefinitionProxies"))
    {
      if (def is not InstanceDefinitionProxy idp)
      {
        throw new InvalidOperationException(
          $"Encountered a {def.GetType()} where expected a {typeof(InstanceDefinitionProxy)}"
        );
      }

      foreach (var appId in idp.objects)
      {
        set.Add(appId);
      }
    }
    return set;
  }
}
