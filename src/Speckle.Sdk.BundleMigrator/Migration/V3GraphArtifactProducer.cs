using System.Collections;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
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

namespace Speckle.Sdk.BundleMigrator.Migration;

/// <summary>
/// Migrates a v3 <see cref="Base"/> graph — one carrying root-level proxies — into the artefact bundle.
///
/// Discovery runs off <see cref="DefaultTraversal"/>, which yields exactly the two node kinds we migrate —
/// every <see cref="Collection"/> and every ATOMIC object — depth-first, parent before child. Traversal only
/// descends the <c>elements</c> lineage, so it halts at an atomic object: its <c>displayValue</c> and data
/// properties are handled here, never walked into.
///
/// Appearance (material/colour) is proxy-driven and preserved on the plane v3 addressed: a mesh ref becomes a
/// geometry-sourced HAS_MATERIAL/HAS_COLOR, an instance-object ref an OBJECT_HAS_* edge, and a layer ref a
/// NODE_HAS_* edge on the collection node — consumers resolve inheritance via the spec's precedence ladders.
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

  // Supports Revit linked models: the v3 connector suffixed every linked element's appId with a per-placement
  // hash, while proxies and properties kept referencing the bare UniqueId (see TryGetLinkedModelSuffix).
  private readonly HashSet<string> _linkedModelSuffixes = new(StringComparer.Ordinal);

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

  // Root-child collections detected as the federation tier, retagged CONTAINER(subtype=Model) instead of
  // COLLECTION; their appId → container K.
  private HashSet<string> _modelCollectionAppIds = new(StringComparer.Ordinal);
  private readonly Dictionary<string, int> _modelContainerByAppId = new(StringComparer.Ordinal);

  // INSTANCE-node K by appId, shared between atomic instance leaves and nested-instance definition members.
  private readonly Dictionary<string, int> _instanceNodeByAppId = new(StringComparer.Ordinal);

  // object appId → its display geometry appIds
  private readonly Dictionary<string, List<string>> _objectDisplayGeoms = new(StringComparer.Ordinal);

  // InstanceProxy atomics: appearance bound to these appIds rides the object plane (rels 26/27).
  private readonly HashSet<string> _instanceObjectAppIds = new(StringComparer.Ordinal);

  // definition-member appId → its raw-solid geometry appId ("<appId>:solid")
  private readonly Dictionary<string, string> _defMemberSolidKey = new(StringComparer.Ordinal);

  // v3 CSi root `analysisResults` (ENG-9076). CSi results key objects by NAME, so when it's present the
  // traversal also collects name → appId (last wins, mirroring big-truck's nameToAppId).
  private Base? _analysisResults;
  private readonly Dictionary<string, string> _objectAppIdByName = new(StringComparer.Ordinal);

  // First non-empty per-object units, in emission order — matches ArtefactBundle.InferUnits on read.
  private string? _graphUnits;

  public Stats Produce(Base root)
  {
    IReadOnlySet<string> defSourceAppIds = GetDefinitionAppIds(root);
    _modelCollectionAppIds = DetectRevitModelCollections(root);
    _analysisResults = (root["analysisResults"] ?? root["@analysisResults"]) as Base;

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
        if (_modelCollectionAppIds.Contains(helper.Aid(col)))
        {
          // Federation tier: a source model is a CONTAINER, not a collection — its child collections become
          // top-level (flat containers, as the v4 Revit builder writes them).
          var mk = pipeline.AddContainer(helper.CollectionKey(col), col.name, null, "Model");
          _modelContainerByAppId[helper.Aid(col)] = mk;
          _stats.Models++;
          continue;
        }

        // Parent is a Collection except at the root, which is skipped above and never mapped → top-level (null).
        int? parentK = _collectionMap.TryGetValue(helper.Aid(parent.Current), out var pk) ? pk : null;
        var ghTopology = ReadGhTopology(col);
        var k = pipeline.AddCollection(
          helper.CollectionKey(col),
          col.name,
          parentK,
          helper.CollectionSubtype(col),
          ghTopology
        );
        _collectionMap[helper.Aid(col)] = k;
        _stats.Collections++;
        if (ghTopology is not null)
        {
          _stats.GhTopologies++;
        }
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

    EmitProxies(root);
    EmitRevitTopology();
    EmitCameraViews(root);
    EmitStructuralResults(root);
    EmitReferencePoint(root);
    EmitPropertySetDefinitions(root);
    EmitSceneView();

    _stats.Geometries = _seenGeometryAppIds.Count;
    pipeline.Complete();
    return _stats;
  }

  // v3 Grasshopper carries its data-tree paths as a dynamic `topology` string on each collection
  // (SpeckleCollectionWrapper.Topology); the bundle carries it verbatim as nodes.gh_topology.
  // The connector writes an explicit null on collections without an authored tree.
  private string? ReadGhTopology(Collection col)
  {
    var raw = col["topology"] ?? col["@topology"];
    if (raw is string s && !string.IsNullOrWhiteSpace(s))
    {
      return s;
    }
    if (raw is not null and not string)
    {
      _stats.Notes.Add($"collection '{col.name}' topology skipped: not a string");
    }
    return null;
  }

  // Every atomic object belongs to its nearest ancestor collection (IN_COLLECTION), regardless of what sits
  // between them. If an object host (e.g. a DataObject carrying `elements`) is one of those in-between nodes,
  // it also gets a host→hosted SUBELEMENT to the nearest such host.
  private void EmitHierarchyEdge(TraversalContext tc, int objK)
  {
    var hostFound = false;
    var collectionFound = false;
    for (var p = tc.Parent; p is not null; p = p.Parent)
    {
      var pc = p.Current;
      if (pc is Collection)
      {
        if (_modelContainerByAppId.TryGetValue(helper.Aid(pc), out var mk))
        {
          pipeline.InModel(objK, mk, 0);
          _stats.InModelEdges++;
          return; // model containers sit directly under the root
        }
        if (!collectionFound)
        {
          collectionFound = true;
          if (_collectionMap.TryGetValue(helper.Aid(pc), out var ck))
          {
            pipeline.InCollection(objK, ck, 0);
            _stats.InCollectionEdges++;
          }
          if (_modelCollectionAppIds.Count == 0)
          {
            return; // no federation tier; nearest collection resolved membership
          }
        }
        continue; // keep climbing to the model tier
      }
      if (!hostFound && !collectionFound && _objectKMap.TryGetValue(pc.id.NotNull(), out var hostK))
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

    if (_analysisResults is not null && obj["name"] is string name)
    {
      _objectAppIdByName[name] = appId;
    }

    if (TryGetLinkedModelSuffix(appId, out var suffix))
    {
      _linkedModelSuffixes.Add(suffix);
    }

    if (_graphUnits is null && obj["units"] is string { Length: > 0 } objUnits)
    {
      _graphUnits = objUnits;
    }

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
      _instanceObjectAppIds.Add(appId);
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
      // Deliberately NOT recorded in _objectDisplayGeoms — materials/colours bind to display meshes, not the solid.
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
            RecordObjectGeom(appId, gAppId);
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
        RecordObjectGeom(appId, appId);
      }
    }

    return objK;
  }

  private void RecordObjectGeom(string objAppId, string geomAppId)
  {
    if (!_objectDisplayGeoms.TryGetValue(objAppId, out var list))
    {
      list = new List<string>();
      _objectDisplayGeoms[objAppId] = list;
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
    // Shared across placements; a proxy ref to the member binds to this geometry-K.
    _objectDisplayGeoms[appId] = new List<string> { appId };
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

  private void EmitProxies(Base root)
  {
    // Binds value-nodes to the plane each v3 proxy ref addressed: an instance object, display geometry,
    // or a collection/container node. First claim wins per target; planes never cross.
    (Dictionary<string, int> ByGeometry, Dictionary<string, int> ByObject, Dictionary<int, int> ByNode) BindByPlane(
      List<(int nodeK, List<string> refs)> proxies,
      out int skipped
    )
    {
      var byGeometry = new Dictionary<string, int>(StringComparer.Ordinal);
      var byObject = new Dictionary<string, int>(StringComparer.Ordinal);
      var byNode = new Dictionary<int, int>();
      skipped = 0;
      foreach (var (nodeK, refs) in proxies)
      {
        foreach (var r in refs)
        {
          if (_instanceObjectAppIds.Contains(r))
          {
            byObject.TryAdd(r, nodeK);
          }
          else if (_objectDisplayGeoms.TryGetValue(r, out var geoms))
          {
            foreach (var g in geoms)
            {
              byGeometry.TryAdd(g, nodeK);
            }
          }
          else if (_seenGeometryAppIds.Contains(r))
          {
            byGeometry.TryAdd(r, nodeK);
          }
          else if (_collectionMap.TryGetValue(r, out var collK) || _modelContainerByAppId.TryGetValue(r, out collK))
          {
            byNode.TryAdd(collK, nodeK);
          }
          else
          {
            skipped++;
          }
        }
      }
      return (byGeometry, byObject, byNode);
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
    var matBind = BindByPlane(matProxies, out var matSkipped);
    _stats.SkippedMaterial += matSkipped;

    var colProxies = new List<(int, List<string>)>();
    foreach (var col in helper.GetBaseList(root, "colorProxies"))
    {
      if (col is not ColorProxy cp)
      {
        continue;
      }
      // AutoCAD ByBlock placeholder (value=-1): binding it would paint shared definition geometry white.
      if (cp["source"] is "block")
      {
        _stats.ByBlockColorProxies++;
        continue;
      }
      colProxies.Add((pipeline.AddColor(cp.value), cp.objects));
      _stats.Colors++;
    }
    var colBind = BindByPlane(colProxies, out var colSkipped);
    _stats.SkippedColor += colSkipped;

    // Placeholder black yields to a real display colour on the same target, per plane.
    foreach (var (gAppId, matK) in matBind.ByGeometry)
    {
      if (placeholderMatKs.Contains(matK) && colBind.ByGeometry.ContainsKey(gAppId))
      {
        continue;
      }
      pipeline.HasMaterial(pipeline.InternGeometryId(gAppId), matK);
      _stats.HasMaterialEdges++;
    }
    foreach (var (objAppId, matK) in matBind.ByObject)
    {
      if (placeholderMatKs.Contains(matK) && colBind.ByObject.ContainsKey(objAppId))
      {
        continue;
      }
      pipeline.ObjectHasMaterial(pipeline.InternObject(objAppId), matK);
      _stats.ObjectHasMaterialEdges++;
    }
    foreach (var (collK, matK) in matBind.ByNode)
    {
      if (placeholderMatKs.Contains(matK) && colBind.ByNode.ContainsKey(collK))
      {
        continue;
      }
      pipeline.NodeHasMaterial(collK, matK);
      _stats.NodeHasMaterialEdges++;
    }
    foreach (var (gAppId, colK) in colBind.ByGeometry)
    {
      pipeline.HasColor(pipeline.InternGeometryId(gAppId), colK);
      _stats.HasColorEdges++;
    }
    foreach (var (objAppId, colK) in colBind.ByObject)
    {
      pipeline.ObjectHasColor(pipeline.InternObject(objAppId), colK);
      _stats.ObjectHasColorEdges++;
    }
    foreach (var (collK, colK) in colBind.ByNode)
    {
      pipeline.NodeHasColor(collK, colK);
      _stats.NodeHasColorEdges++;
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
        var resolvedAny = false;
        foreach (var resolved in ResolveMemberRefs(objAppId))
        {
          pipeline.OnLevel(pipeline.InternObject(resolved), lvlK);
          _stats.OnLevelEdges++;
          resolvedAny = true;
        }
        if (!resolvedAny)
        {
          _stats.SkippedLevel++;
        }
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
        var resolvedAny = false;
        foreach (var resolved in ResolveMemberRefs(objAppId))
        {
          members.Add(pipeline.InternObject(resolved));
          resolvedAny = true;
        }
        if (!resolvedAny)
        {
          _stats.SkippedGroup++;
        }
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

  // v3 Revit federated sends (SendCollectionManager nested mode) put one collection per source model under
  // the root: the host model named exactly like the root, links named by file. The collections carry no
  // marker, so require both signals — the host wrapper by name, and a sibling holding linked-model elements.
  // Non-federated sends stay untouched.
  private HashSet<string> DetectRevitModelCollections(Base root)
  {
    var result = new HashSet<string>(StringComparer.Ordinal);
    if (root is not Collection rootCol || string.IsNullOrEmpty(rootCol.name))
    {
      return result;
    }

    var children = rootCol.elements.OfType<Collection>().ToList();
    var main = children.FirstOrDefault(c => c.name == rootCol.name);
    if (main is null)
    {
      return result;
    }

    var links = children
      .Where(c => !ReferenceEquals(c, main) && c.name != "definitionGeometry" && HasLinkedModelElements(c))
      .ToList();
    if (links.Count == 0)
    {
      return result;
    }

    result.Add(helper.Aid(main));
    foreach (var link in links)
    {
      result.Add(helper.Aid(link));
    }
    return result;
  }

  private static bool HasLinkedModelElements(Collection col)
  {
    foreach (var el in col.elements)
    {
      if (el.applicationId is { } aid && TryGetLinkedModelSuffix(aid, out _))
      {
        return true;
      }
      if (el is Collection sub && HasLinkedModelElements(sub))
      {
        return true;
      }
    }
    return false;
  }

  // The v3 Revit connector kept one appId per placement of a linked model by suffixing the element's
  // UniqueId with a hash of the link's placement transform: {UniqueId}_t{8 lowercase hex}
  // (LinkedModelHandler.GetTransformHash).
  private static bool TryGetLinkedModelSuffix(string appId, out string suffix)
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

  // An exact match covers everything except Revit linked models, whose objects were sent suffixed while
  // proxies reference the bare UniqueId — there a bare ref matches once per placement of the link.
  private IEnumerable<string> ResolveMemberRefs(string bareRef)
  {
    if (_seenObjectAppIds.Contains(bareRef))
    {
      yield return bareRef;
      yield break;
    }
    foreach (var suffix in _linkedModelSuffixes)
    {
      if (_seenObjectAppIds.Contains(bareRef + suffix))
      {
        yield return bareRef + suffix;
      }
    }
  }

  // A Revit element and the room/host its properties reference live in the same document, so on a
  // linked-model send both carry the same suffix — a missed bare ref is re-tried with the element's own.
  private bool TryResolveRef(string bareRef, string elementAppId, out string resolved)
  {
    if (_seenObjectAppIds.Contains(bareRef))
    {
      resolved = bareRef;
      return true;
    }
    if (TryGetLinkedModelSuffix(elementAppId, out var suffix) && _seenObjectAppIds.Contains(bareRef + suffix))
    {
      resolved = bareRef + suffix;
      return true;
    }
    resolved = "";
    return false;
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

  // v3 CSi commits: root `analysisResults` → structural_results rows, unit scalars → eav.model (ENG-9076).
  private void EmitStructuralResults(Base root)
  {
    var migrator = new StructuralResultsMigrator(pipeline, _stats);
    migrator.MigrateModelScalars(root);
    if (_analysisResults is not null)
    {
      migrator.MigrateResults(_analysisResults, _objectAppIdByName);
    }
  }

  // Re-emits the v3 Revit root `referencePointTransform` (translation at 12–14, internal feet) as the spec's
  // referencePoint.* rows — the inverse of ObjectsArtifactReader.BuildReferencePointRootValue. No kind row: v3 never recorded it.
  private void EmitReferencePoint(Base root)
  {
    var raw = root["referencePointTransform"] ?? root["@referencePointTransform"];
    if (raw is null)
    {
      return; // internal-origin send: no rows
    }
    if (ReadReferencePointMatrix(raw) is not { } m)
    {
      _stats.Notes.Add("root referencePointTransform skipped: not a 16-double rigid transform");
      return;
    }

    var units = _graphUnits;
    double toDisplay;
    if (units is not null && Units.IsUnitSupported(units))
    {
      toDisplay = Units.GetConversionFactor(Units.Feet, units);
    }
    else
    {
      // No convertible display units in the graph — the explicit units row keeps the value self-describing.
      _stats.Notes.Add($"referencePoint kept in ft: graph units '{units ?? "none"}' unsupported");
      units = Units.Feet;
      toDisplay = 1;
    }

    double[] d =
    [
      m[0],
      m[4],
      m[8],
      m[12] * toDisplay,
      m[1],
      m[5],
      m[9],
      m[13] * toDisplay,
      m[2],
      m[6],
      m[10],
      m[14] * toDisplay,
      0,
      0,
      0,
      1,
    ];
    pipeline.AddModelProperty(
      "referencePoint.transform",
      string.Join(",", d.Select(v => v.ToString("R", CultureInfo.InvariantCulture)))
    );
    pipeline.AddModelProperty("referencePoint.units", units);
    _stats.ReferencePoints++;
  }

  private const double MatrixTolerance = 1e-9;

  // { "transform": [16 numbers] } with the affine padding the v3 writer always emits (0 at 3/7/11, 1 at 15);
  // anything else is not ReferencePointHelper.CreateTransformDataForRootObject's output.
  private static double[]? ReadReferencePointMatrix(object raw)
  {
    if (
      raw is not Dictionary<string, object?> data
      || !data.TryGetValue("transform", out var t)
      || t is string
      || t is not IEnumerable values
    )
    {
      return null;
    }
    var m = new List<double>(16);
    foreach (var v in values)
    {
      double? item = v switch
      {
        double dbl => dbl,
        long l => l,
        int i => i,
        _ => null,
      };
      if (item is not { } dv || !double.IsFinite(dv) || m.Count == 16)
      {
        return null;
      }
      m.Add(dv);
    }
    if (
      m.Count != 16
      || Math.Abs(m[3]) > MatrixTolerance
      || Math.Abs(m[7]) > MatrixTolerance
      || Math.Abs(m[11]) > MatrixTolerance
      || Math.Abs(m[15] - 1) > MatrixTolerance
    )
    {
      return null;
    }
    return m.ToArray();
  }

  // v3 Civil3D root `propertySetDefinitions` → eav.property_set_definitions rows, one per field in dict
  // (authored) order. Values already ride eav at properties.Property Sets.*; attachment derives from those paths.
  private void EmitPropertySetDefinitions(Base root)
  {
    var raw = root["propertySetDefinitions"] ?? root["@propertySetDefinitions"];
    if (raw is null)
    {
      return;
    }
    if (raw is not Dictionary<string, object?> sets)
    {
      _stats.Notes.Add("root propertySetDefinitions skipped: not a dictionary");
      return;
    }

    foreach (var (setName, setValue) in sets)
    {
      if (
        setValue is not Dictionary<string, object?> set
        || set.GetValueOrDefault("propertyDefinitions") is not Dictionary<string, object?> { Count: > 0 } fieldDefs
      )
      {
        _stats.Notes.Add($"property set '{setName}' skipped: no field definitions");
        continue;
      }

      var setKey = ComputePropertySetKey(setName, fieldDefs);
      var emitted = 0;
      foreach (var (fieldName, fieldValue) in fieldDefs)
      {
        if (fieldValue is not Dictionary<string, object?> fd)
        {
          _stats.Notes.Add($"property set '{setName}' field '{fieldName}' skipped: not a definition");
          continue;
        }
        var (defaultString, defaultDouble, defaultBoolean) = SplitPropertyDefault(fd.GetValueOrDefault("defaultValue"));
        pipeline.AddPropertySetDefinition(
          setName,
          setKey,
          fieldName,
          fieldBucketId: null, // not recorded by v3; consumers fall back to matching fieldName
          fd.GetValueOrDefault("dataType") as string,
          defaultString,
          defaultDouble,
          defaultBoolean,
          fd.GetValueOrDefault("units") as string,
          fd.GetValueOrDefault("description") as string
        );
        emitted++;
      }
      if (emitted > 0)
      {
        _stats.PropertySets++;
        _stats.PropertySetFields += emitted;
      }
    }
  }

  // Must stay byte-identical with PropertySetDefinitionLadder.ComputeSetKey (speckle-sharp-connectors) and
  // dwgextract: sha256_hex_uppercase(setName + "\n" + join("\n", field|dataType|unit) in field order).
  private static string ComputePropertySetKey(string setName, Dictionary<string, object?> fieldDefs)
  {
    var parts = new List<string> { setName };
    foreach (var (fieldName, fieldValue) in fieldDefs)
    {
      if (fieldValue is Dictionary<string, object?> fd)
      {
        parts.Add(
          $"{fieldName}|{fd.GetValueOrDefault("dataType") as string}|{fd.GetValueOrDefault("units") as string}"
        );
      }
    }
    return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", parts))));
  }

  // The file's exactly-one-of defaults rule, split the way the native v4 send does.
  private static (string? S, double? D, bool? B) SplitPropertyDefault(object? value) =>
    value switch
    {
      null => (null, null, null),
      bool b => (null, null, b),
      IConvertible c and not string => (null, Convert.ToDouble(c, CultureInfo.InvariantCulture), null),
      _ => (value.ToString() is { Length: > 0 } s ? s : null, null, null),
    };

  private void EmitSceneView()
  {
    var keys = new List<SceneViewKey>();
    // Model tier only when the send actually federates >1 source model (mirrors the v4 Revit builder).
    if (_modelContainerByAppId.Count > 1)
    {
      keys.Add(SceneViewKey.Rel(RelKind.InModel));
    }
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
