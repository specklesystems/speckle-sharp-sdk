using System.Globalization;
using Speckle.DoubleNumerics;
using Speckle.Objects.Data;
using Speckle.Objects.Other;
using Speckle.Objects.Utils;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;
using Speckle.Sdk.Models.Instances;
using Speckle.Sdk.Models.Proxies;

namespace Speckle.Sdk.Artifacts.Harness;

/// <summary>
/// FIRST-CUT migration driver: walks a *stored* Speckle <see cref="Base"/> graph (the kind a connector
/// sent — collections + atomic objects with displayValue + root-collection proxy arrays) and drives the
/// validated <see cref="ObjectsArtifactPipeline"/> to emit the 3-artefact bundle (geometries + eav + envelope).
///
/// This is the analogue of ODA's <c>RevitModelExtractor.EmitObject</c> + the post-loop value-node emission,
/// but reading a deserialized graph instead of live ODA elements. It lives in the harness while we tune
/// derivation correctness against real NDJSONs; promote to <c>src/Speckle.Objects/Utils</c> once proven.
///
/// KNOWN OPEN KEYING CHOICES (the "settle at implementation" items from topology-envelope-SOT §7) — first
/// cut picks the ODA/BIM-consistent option and reports mismatches so we can validate against real data:
///   • definition.objects[]      → geometry namespace (InternGeometryId)
///   • material.objects[]        → geometry namespace (per-mesh; CAD is object-grained — flagged in stats)
///   • color.objects[]           → object namespace
///   • level.objects[]           → object namespace
/// </summary>
public static class GraphArtifactProducer
{
  public sealed class Stats
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
    public int DefinitionGeometries;
    public int DefinitionInstances;
    public int DefinesInstanceEdges;
    public int MeshAtomics;
    public int InstanceAtomics;
    public int GeometryEncodeFailures;

    // Proxy refs skipped because the target appId is absent from the graph (e.g. level membership to a
    // Revit Room filtered out of the export). Skipped rather than minting a phantom object/geometry K.
    public int SkippedDefines;
    public int SkippedMaterial;
    public int SkippedColor;
    public int SkippedLevel;
    public int SkippedDangling => SkippedDefines + SkippedMaterial + SkippedColor + SkippedLevel;
    public readonly List<string> Notes = new();

    public override string ToString() =>
      $"""
      objects={Objects} (meshAtomic={MeshAtomics} instAtomic={InstanceAtomics})  geometries={Geometries} (defGeom={DefinitionGeometries})  encodeFailures={GeometryEncodeFailures}
      edges: DISPLAY={DisplayEdges} DISPLAY_INSTANCE={DisplayInstanceEdges} SUBELEMENT={SubelementEdges}
             DEFINES={DefinesEdges} DEFINES_INSTANCE={DefinesInstanceEdges} HAS_MATERIAL={HasMaterialEdges} HAS_COLOR={HasColorEdges} ON_LEVEL={OnLevelEdges}
      nodes: DEFINITION={Definitions} INSTANCE(def)={DefinitionInstances} MATERIAL={Materials} COLOR={Colors} LEVEL={Levels}
      skipped (ref not in graph): {SkippedDangling}  (DEFINES={SkippedDefines} HAS_MATERIAL={SkippedMaterial} HAS_COLOR={SkippedColor} ON_LEVEL={SkippedLevel})
      """;
  }

  public static Stats Produce(Base root, string outDir, string baseName)
  {
    Directory.CreateDirectory(outDir);
    var stats = new Stats();
    using var pipeline = new ObjectsArtifactPipeline(outDir, baseName);

    // appIds we have actually emitted as objects / geometry — to detect dangling proxy refs.
    var seenObjectAppIds = new HashSet<string>(StringComparer.Ordinal);
    var seenGeometryAppIds = new HashSet<string>(StringComparer.Ordinal);
    // INSTANCE-node K by appId — for both atomic instance leaves and nested-instance definition members,
    // so the DEFINES_INSTANCE (rel 9) edges from a definition to a nested instance resolve to the node.
    var instanceNodeByAppId = new Dictionary<string, int>(StringComparer.Ordinal);
    // object appId → the geometry appIds of its DISPLAY meshes — lets OBJECT-grained material/colour proxy
    // refs (which name the DataObject, not its mesh — Navis/CAD) bind to the object's display geometry.
    var objectDisplayGeomKeys = new Dictionary<string, List<string>>(StringComparer.Ordinal);

    // Instance-definition source members: appIds in `instanceDefinitionProxies[].objects[]`. These are the
    // definition's content (rendered via instance placements, never directly), NOT atomic scene objects —
    // route each to GEOMETRY (raw-mesh member) or to an INSTANCE node (nested-instance member), so
    // DEFINES/DEFINES_INSTANCE + HAS_MATERIAL resolve. Mirrors ODA's DefinitionGeometries.
    var defSourceAppIds = CollectDefinitionSourceAppIds(root);

    // 1) Walk the collection tree. Definition members → geometry/instance-node; every other leaf is an
    //    atomic object — type-agnostically (SOT §1): DataObject, raw mesh, or bare instance.
    foreach (var (obj, parent) in TraverseAtomics(root))
    {
      if (defSourceAppIds.Contains(Aid(obj)))
      {
        EmitDefinitionMember(pipeline, obj, stats, seenGeometryAppIds, instanceNodeByAppId);
        continue;
      }

      var objK = EmitObject(pipeline, obj, stats, seenObjectAppIds, seenGeometryAppIds, instanceNodeByAppId, objectDisplayGeomKeys);

      // host→hosted nesting: a non-collection object carrying `elements` (e.g. curtain wall → panels).
      foreach (var child in GetBaseList(obj, "elements"))
      {
        if (child is Collection || defSourceAppIds.Contains(Aid(child)))
        {
          continue;
        }
        var childK = EmitObject(pipeline, child, stats, seenObjectAppIds, seenGeometryAppIds, instanceNodeByAppId, objectDisplayGeomKeys);
        pipeline.Subelement(objK, childK, stats.SubelementEdges++);
      }
      _ = parent;
    }

    // layer/collection appId → the geometry of all its descendant objects — for ByLayer material/colour
    // (CAD proxies reference a Layer; resolve the walk-up at PRODUCE so the consumer stays flat).
    var layerGeomKeys = BuildLayerGeomKeys(root, objectDisplayGeomKeys);

    // 2) Value-nodes + their edges, from the root collection's proxy arrays.
    EmitProxies(pipeline, root, stats, seenObjectAppIds, seenGeometryAppIds, instanceNodeByAppId, objectDisplayGeomKeys, layerGeomKeys);

    stats.Geometries = seenGeometryAppIds.Count;

    pipeline.Complete();
    return stats;
  }

  // ── object emission ──────────────────────────────────────────────────────────────

  private static int EmitObject(
    ObjectsArtifactPipeline pipeline,
    Base obj,
    Stats stats,
    HashSet<string> seenObjectAppIds,
    HashSet<string> seenGeometryAppIds,
    Dictionary<string, int> instanceNodeByAppId,
    Dictionary<string, List<string>> objectDisplayGeomKeys
  )
  {
    var appId = Aid(obj);
    var objK = pipeline.InternObject(appId);
    if (!seenObjectAppIds.Add(appId))
    {
      return objK; // already emitted (shared reference) — same K, idempotent intern
    }

    stats.Objects++;

    var (props, rootScalars, typeKey) = ExtractProperties(obj);
    pipeline.AddProperties(appId, props, rootScalars, typeKey);

    // SOT §1 — atomic is type-agnostic; dispatch on the leaf shape:
    if (obj is InstanceProxy ip)
    {
      // instance-atomic: the leaf IS a placement → object DISPLAY_INSTANCEs its own INSTANCE node.
      var instK = ResolveInstanceNode(pipeline, ip, instanceNodeByAppId);
      pipeline.DisplayInstance(objK, instK, 0);
      stats.DisplayInstanceEdges++;
      stats.InstanceAtomics++;
      // Appearance target is the OBJECT itself (ByBlock): the placed definition geometry is shared, so a
      // ByLayer colour must ride on the instance object — the consumer applies it during instance placement.
      RecordObjectGeom(objectDisplayGeomKeys, appId, "o:" + appId);
      return objK;
    }

    // A leaf that carries displayValue references its geometry there — covers DataObjects AND geometry
    // leaves that aren't directly SGEO-encodable but ship a display mesh (Rhino BrepX/SubD, extrusions…).
    // Checked BEFORE the raw-geometry case so we encode the display mesh, not the un-encodable parent.
    var displayValue = GetBaseList(obj, "displayValue").ToList();
    if (displayValue.Count > 0)
    {
      int ord = 0;
      foreach (var item in displayValue)
      {
        if (item is InstanceProxy dip)
        {
          var dInstK = ResolveInstanceNode(pipeline, dip, instanceNodeByAppId);
          pipeline.DisplayInstance(objK, dInstK, ord++);
          stats.DisplayInstanceEdges++;
        }
        else
        {
          var gAppId = GeometryKey(item);
          if (TryAddGeometry(pipeline, gAppId, item, stats, seenGeometryAppIds))
          {
            pipeline.Display(objK, pipeline.InternGeometryId(gAppId), ord++);
            stats.DisplayEdges++;
            RecordObjectGeom(objectDisplayGeomKeys, appId, "g:" + gAppId);
          }
        }
      }
      return objK;
    }

    if (IsGeometry(obj))
    {
      // mesh-atomic: the leaf IS the geometry → DISPLAY(object-K → its own geometry-K). The appId interns
      // into BOTH the object and geometry namespaces (separate counters, no collision).
      if (TryAddGeometry(pipeline, appId, obj, stats, seenGeometryAppIds))
      {
        pipeline.Display(objK, pipeline.InternGeometryId(appId), 0);
        stats.DisplayEdges++;
        stats.MeshAtomics++;
        RecordObjectGeom(objectDisplayGeomKeys, appId, "g:" + appId);
      }
    }

    // else: a non-geometry leaf with no displayValue — an eav object with no renderable geometry.
    return objK;
  }

  // Records that object `objAppId` renders geometry `geomAppId` — so an OBJECT-grained material/colour ref
  // (proxies that name the DataObject, not its mesh) can bind to the object's display meshes.
  private static void RecordObjectGeom(
    Dictionary<string, List<string>> map,
    string objAppId,
    string geomAppId
  )
  {
    if (!map.TryGetValue(objAppId, out var list))
    {
      list = new List<string>();
      map[objAppId] = list;
    }
    list.Add(geomAppId);
  }

  // Intern/emit an INSTANCE node for a placement (idempotent by appId), recording its K so DEFINES_INSTANCE
  // edges to a nested instance can resolve. AddDefinition links it to its own DEFINITION via def_ref.
  private static int ResolveInstanceNode(
    ObjectsArtifactPipeline pipeline,
    InstanceProxy ip,
    Dictionary<string, int> instanceNodeByAppId
  )
  {
    var key = Aid(ip);
    if (instanceNodeByAppId.TryGetValue(key, out var existing))
    {
      return existing;
    }
    var defK = pipeline.AddDefinition(ip.definitionId, ip.definitionId);
    var instK = pipeline.AddInstance(key, defK, Flatten(ip.transform), ip.units);
    instanceNodeByAppId[key] = instK;
    return instK;
  }

  // Encode a geometry blob under `appId` (once). Returns false (and counts) on encode failure.
  private static bool TryAddGeometry(
    ObjectsArtifactPipeline pipeline,
    string appId,
    Base geometry,
    Stats stats,
    HashSet<string> seenGeometryAppIds
  )
  {
    try
    {
      pipeline.AddGeometry(appId, geometry);
      seenGeometryAppIds.Add(appId);
      return true;
    }
    catch (Exception ex)
    {
      stats.GeometryEncodeFailures++;
      if (stats.Notes.Count < 20)
      {
        stats.Notes.Add($"encode fail [{geometry.speckle_type}]: {ex.Message}");
      }
      return false;
    }
  }

  // The union of all appIds referenced by the root's instance-definition proxies — i.e. the definition
  // source geometry that must live in the geometry namespace, not the object set.
  private static HashSet<string> CollectDefinitionSourceAppIds(Base root)
  {
    var set = new HashSet<string>(StringComparer.Ordinal);
    foreach (var def in GetBaseList(root, "instanceDefinitionProxies"))
    {
      if (def is InstanceDefinitionProxy idp)
      {
        foreach (var appId in idp.objects)
        {
          set.Add(appId);
        }
      }
    }
    return set;
  }

  // A definition member (referenced by instanceDefinitionProxies[].objects[]) is definition CONTENT, not an
  // atomic scene object. Route by shape: a raw mesh → geometry blob (linked later via DEFINES); a nested
  // instance → its own INSTANCE node (linked via DEFINES_INSTANCE). A display-bearing member contributes its
  // first display mesh under the member's appId.
  private static void EmitDefinitionMember(
    ObjectsArtifactPipeline pipeline,
    Base obj,
    Stats stats,
    HashSet<string> seenGeometryAppIds,
    Dictionary<string, int> instanceNodeByAppId
  )
  {
    var appId = Aid(obj);

    if (obj is InstanceProxy ip)
    {
      // nested-instance member → INSTANCE node; the outer definition links it via DEFINES_INSTANCE.
      if (!instanceNodeByAppId.ContainsKey(appId))
      {
        ResolveInstanceNode(pipeline, ip, instanceNodeByAppId);
        stats.DefinitionInstances++;
      }
      return;
    }

    if (!seenGeometryAppIds.Add(appId))
    {
      return;
    }
    // Prefer a display mesh (encodable) over the parent geometry, which may be an un-encodable Brep/SubD.
    var geometry =
      GetBaseList(obj, "displayValue").FirstOrDefault(d => d is not InstanceProxy)
      ?? (IsGeometry(obj) ? obj : null);
    if (geometry is null)
    {
      seenGeometryAppIds.Remove(appId);
      stats.SkippedDefines++;
      if (stats.Notes.Count < 20)
      {
        stats.Notes.Add($"def member {appId} has no encodable geometry [{obj.speckle_type}]");
      }
      return;
    }
    try
    {
      pipeline.AddGeometry(appId, geometry);
      stats.DefinitionGeometries++;
    }
    catch (Exception ex)
    {
      stats.GeometryEncodeFailures++;
      if (stats.Notes.Count < 20)
      {
        stats.Notes.Add($"def geom encode fail [{obj.speckle_type}]: {ex.Message}");
      }
    }
  }

  // ── proxy / value-node emission ─────────────────────────────────────────────────

  private static void EmitProxies(
    ObjectsArtifactPipeline pipeline,
    Base root,
    Stats stats,
    HashSet<string> seenObjectAppIds,
    HashSet<string> seenGeometryAppIds,
    Dictionary<string, int> instanceNodeByAppId,
    Dictionary<string, List<string>> objectDisplayGeomKeys,
    Dictionary<string, List<string>> layerGeomKeys
  )
  {
    // DIRECT resolve of a proxy ref to TAGGED appearance targets ("g:<geomAppId>" mesh | "o:<objAppId>"
    // instance-object for ByBlock). (1) names the mesh; (2) names a DataObject/instance → its targets.
    // NOT the layer tier — that's applied second, lower precedence, in BindWithPrecedence.
    IReadOnlyList<string> DirectGeomKeys(string refAppId)
    {
      if (objectDisplayGeomKeys.TryGetValue(refAppId, out var og))
      {
        return og; // already tagged g:/o:
      }
      if (seenGeometryAppIds.Contains(refAppId))
      {
        return new[] { "g:" + refAppId };
      }
      return System.Array.Empty<string>();
    }

    // Bind nodes (material/colour) to geometry with CAD walk-up precedence: a DIRECT (mesh/object) ref
    // claims a geometry first; a Layer ref then fills only geometry not already directly claimed — so an
    // object's own colour overrides its layer's. Returns geomAppId → nodeK (deduped: one per mesh).
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
      foreach (var (nodeK, refs) in proxies)
      {
        foreach (var r in refs)
        {
          if (layerGeomKeys.TryGetValue(r, out var lg))
          {
            foreach (var gk in lg)
            {
              byGeom.TryAdd(gk, nodeK);
            }
          }
        }
      }
      skipped = skip;
      return byGeom;
    }

    // Definitions: members are either raw geometry → DEFINES (rel 4, → geometry), or nested instances →
    // DEFINES_INSTANCE (rel 9, → INSTANCE node). The rel fixes the dst namespace (gap 2).
    foreach (var def in GetBaseList(root, "instanceDefinitionProxies"))
    {
      if (def is not InstanceDefinitionProxy idp)
      {
        continue;
      }
      var defK = pipeline.AddDefinition(DefinitionKey(idp), idp.name);
      stats.Definitions++;
      int o = 0;
      foreach (var memberAppId in idp.objects)
      {
        if (instanceNodeByAppId.TryGetValue(memberAppId, out var instK))
        {
          pipeline.DefinesInstance(defK, instK, o++);
          stats.DefinesInstanceEdges++;
        }
        else if (seenGeometryAppIds.Contains(memberAppId))
        {
          pipeline.Defines(defK, pipeline.InternGeometryId(memberAppId), o++);
          stats.DefinesEdges++;
        }
        else
        {
          // member geometry isn't in the bundle — skip rather than mint a phantom K.
          stats.SkippedDefines++;
        }
      }
    }

    // Materials: HAS_MATERIAL(mesh → material), per-mesh, deduped with object>layer precedence. objects[]
    // may name the mesh (BIM), the OBJECT (object-grained), or a Layer (ByLayer, resolved at produce).
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
      stats.Materials++;
      // A pure-black diffuse is the CAD "no material / ByLayer" placeholder — must not override a real
      // display colour (the loader applies HAS_MATERIAL over HAS_COLOR). Real materials (Rhino tans etc.)
      // are kept; only the black placeholder yields to colour.
      if ((v.diffuse & 0xFFFFFF) == 0)
      {
        placeholderMatKs.Add(matK);
      }
    }
    var matBindings = BindWithPrecedence(matProxies, out var matSkipped);
    stats.SkippedMaterial += matSkipped;

    // Colors: HAS_COLOR(mesh → colour), per-mesh — same 3-tier resolve + object>layer precedence.
    var colProxies = new List<(int, List<string>)>();
    foreach (var col in GetBaseList(root, "colorProxies"))
    {
      if (col is not ColorProxy cp)
      {
        continue;
      }
      colProxies.Add((pipeline.AddColor(cp.value), cp.objects));
      stats.Colors++;
    }
    var colBindings = BindWithPrecedence(colProxies, out var colSkipped);
    stats.SkippedColor += colSkipped;

    // Emit. Targets are tagged "g:<geomAppId>" (mesh) or "o:<objAppId>" (instance object, ByBlock).
    // MATERIAL (rel 5) is geometry-only; also drop a placeholder-black material on a mesh that has a real
    // display colour so the colour wins.
    foreach (var (target, matK) in matBindings)
    {
      if (target[0] != 'g')
      {
        continue; // instances don't carry a per-mesh material
      }
      if (placeholderMatKs.Contains(matK) && colBindings.ContainsKey(target))
      {
        continue;
      }
      pipeline.HasMaterial(pipeline.InternGeometryId(target[2..]), matK);
      stats.HasMaterialEdges++;
    }
    // COLOUR (rel 6, src_ns = geometry|object): direct mesh → on the geometry; instance → on the OBJECT,
    // which the consumer applies to the placed (shared) definition geometry during instance expansion.
    foreach (var (target, colK) in colBindings)
    {
      var appId = target[2..];
      if (target[0] == 'o')
      {
        pipeline.HasColor(pipeline.InternObject(appId), colK);
      }
      else
      {
        pipeline.HasColor(pipeline.InternGeometryId(appId), colK);
      }
      stats.HasColorEdges++;
    }

    // Levels: ON_LEVEL(object → level). Generic read (no strong-typed LevelProxy in the SDK).
    // Refs to elements absent from the export (e.g. Revit Rooms in a "sans rooms" send) are skipped.
    foreach (var lvl in GetBaseList(root, "levelProxies"))
    {
      var members = lvl.GetMembers(DynamicBaseMemberType.Instance | DynamicBaseMemberType.Dynamic);
      var (name, elevation) = ReadLevelValue(members);
      var lvlK = pipeline.AddLevel(LevelKey(lvl, name), name, elevation);
      stats.Levels++;
      foreach (var objAppId in AsStringList(members.GetValueOrDefault("objects")))
      {
        if (!seenObjectAppIds.Contains(objAppId))
        {
          stats.SkippedLevel++;
          continue;
        }
        pipeline.OnLevel(pipeline.InternObject(objAppId), lvlK);
        stats.OnLevelEdges++;
      }
    }
  }

  // ── traversal ────────────────────────────────────────────────────────────────────

  // Members that hold geometry or proxy data, NOT child objects — never descended as containers.
  // (`displayValue` is the object's own geometry, handled in EmitObject; the proxy arrays live on the root.)
  private static readonly HashSet<string> s_nonContainerMembers = new(StringComparer.Ordinal)
  {
    "displayValue", "@displayValue", "baseCurves", "@baseCurves",
    "instanceDefinitionProxies", "@instanceDefinitionProxies",
    "renderMaterialProxies", "@renderMaterialProxies",
    "colorProxies", "@colorProxies", "levelProxies", "@levelProxies",
    "groupProxies", "@groupProxies",
    // non-scene metadata containers (not renderable model objects)
    "cameras", "@cameras", "views", "@views",
  };

  // Yields every LEAF reachable through any container member, with its parent. A leaf is atomic
  // type-agnostically (SOT §1) — DataObject, raw geometry, or a bare InstanceProxy placement. Descent
  // follows `elements` AND any other Base-list member (e.g. Civil3D `@mainBaselineFeatureLines`, `@regions`)
  // except geometry/proxy members — so awkwardly-nested geometry is reached, not just `elements`.
  private static IEnumerable<(Base obj, Base? parent)> TraverseAtomics(Base root)
  {
    var stack = new Stack<(Base node, Base? parent)>();
    var visited = new HashSet<string>(StringComparer.Ordinal);
    stack.Push((root, null));
    while (stack.Count > 0)
    {
      var (node, parent) = stack.Pop();
      if (node.id is { } id && !visited.Add(id))
      {
        continue; // already walked (shared reference) — avoid re-descending
      }
      var isContainer = node is Collection || ReferenceEquals(node, root);
      if (!isContainer && !IsValueOrDefinitionProxy(node))
      {
        yield return (node, parent);
      }
      // Geometry is TERMINAL — never descend into it. Its list-members (Polycurve `segments`, Mesh
      // `vertices`/`faces`, etc.) are internal components, not child scene objects; descending would extract
      // them as spurious fragment-objects (and they'd miss layer-colour resolution).
      if (IsGeometry(node))
      {
        continue;
      }
      foreach (var child in ChildContainers(node))
      {
        stack.Push((child, node));
      }
    }
  }

  // layer/collection appId → the display-geometry appIds of every object beneath it (any depth). Lets a
  // ByLayer material/colour proxy (which references a Layer) bind to that layer's objects' meshes — the
  // CAD "walk up to the layer's colour" resolved once at produce, keeping the envelope flat.
  private static Dictionary<string, List<string>> BuildLayerGeomKeys(
    Base root,
    Dictionary<string, List<string>> objectDisplayGeomKeys
  )
  {
    var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
    void Walk(Base node)
    {
      foreach (var child in ChildContainers(node))
      {
        if (child is Collection)
        {
          var geoms = new List<string>();
          CollectDescendantGeom(child, objectDisplayGeomKeys, geoms);
          if (geoms.Count > 0)
          {
            result[Aid(child)] = geoms;
          }
          Walk(child);
        }
      }
    }
    Walk(root);
    return result;
  }

  private static void CollectDescendantGeom(
    Base collection,
    Dictionary<string, List<string>> objectDisplayGeomKeys,
    List<string> acc
  )
  {
    foreach (var child in ChildContainers(collection))
    {
      if (child is Collection)
      {
        CollectDescendantGeom(child, objectDisplayGeomKeys, acc);
      }
      else if (objectDisplayGeomKeys.TryGetValue(Aid(child), out var gks))
      {
        acc.AddRange(gks);
      }
    }
  }

  // All child Base objects under `node`'s container members (every Base-list member except the
  // geometry/proxy members in s_nonContainerMembers).
  private static IEnumerable<Base> ChildContainers(Base node)
  {
    var members = node.GetMembers(DynamicBaseMemberType.Instance | DynamicBaseMemberType.Dynamic);
    foreach (var kv in members)
    {
      if (s_nonContainerMembers.Contains(kv.Key))
      {
        continue;
      }
      if (kv.Value is System.Collections.IEnumerable seq and not string)
      {
        foreach (var item in seq)
        {
          if (item is Base b)
          {
            yield return b;
          }
        }
      }
    }
  }

  // ── property extraction ──────────────────────────────────────────────────────────

  private static (
    IReadOnlyDictionary<string, object?> props,
    IEnumerable<KeyValuePair<string, object?>> rootScalars,
    string? typeKey
  ) ExtractProperties(Base obj)
  {
    IReadOnlyDictionary<string, object?> props =
      obj is DataObject dobj
        ? dobj.properties
        : obj.GetMembers(DynamicBaseMemberType.Instance | DynamicBaseMemberType.Dynamic);

    var members = obj.GetMembers(DynamicBaseMemberType.Instance | DynamicBaseMemberType.Dynamic);
    var rootScalars = new List<KeyValuePair<string, object?>>
    {
      new("speckle_type", obj.speckle_type),
      new("name", members.GetValueOrDefault("name")),
      new("units", members.GetValueOrDefault("units")),
      new("category", members.GetValueOrDefault("category")),
      new("family", members.GetValueOrDefault("family")),
      new("type", members.GetValueOrDefault("type")),
    };

    var typeKey =
      members.GetValueOrDefault("typeId") as string
      ?? (props.TryGetValue("typeId", out var tk) ? tk as string : null);

    return (props, rootScalars, typeKey);
  }

  private static (string? name, double elevation) ReadLevelValue(IReadOnlyDictionary<string, object?> members)
  {
    if (members.GetValueOrDefault("value") is Base valueBase)
    {
      var vm = valueBase.GetMembers(DynamicBaseMemberType.Instance | DynamicBaseMemberType.Dynamic);
      return (vm.GetValueOrDefault("name") as string, ToDouble(vm.GetValueOrDefault("elevation")));
    }
    if (members.GetValueOrDefault("value") is IReadOnlyDictionary<string, object?> vd)
    {
      return (vd.GetValueOrDefault("name") as string, ToDouble(vd.GetValueOrDefault("elevation")));
    }
    return (members.GetValueOrDefault("name") as string, ToDouble(members.GetValueOrDefault("elevation")));
  }

  // ── id keying (everything is applicationId-keyed; null → stable per-object key) ───

  private static string Aid(Base b) => b.applicationId ?? "spk:" + b.id;

  private static string GeometryKey(Base mesh) => mesh.applicationId ?? "spk:" + mesh.id;

  private static string DefinitionKey(InstanceDefinitionProxy idp) => idp.applicationId ?? idp.name;

  private static string MaterialKey(RenderMaterialProxy rmp) =>
    rmp.applicationId ?? rmp.value.applicationId ?? "mat:" + rmp.value.diffuse.ToString(CultureInfo.InvariantCulture);

  private static string LevelKey(Base lvl, string? name) => lvl.applicationId ?? "lvl:" + (name ?? lvl.id);

  // ── helpers ──────────────────────────────────────────────────────────────────────

  // Value/definition proxies that are NEVER atomic leaves (they live on the root's dynamic members).
  // Note: InstanceProxy is deliberately excluded — a bare instance placement CAN be an atomic leaf.
  private static bool IsValueOrDefinitionProxy(Base b) =>
    b is InstanceDefinitionProxy or RenderMaterialProxy or ColorProxy or GroupProxy;

  private static bool IsGeometry(Base b) =>
    b.speckle_type.StartsWith("Objects.Geometry.", StringComparison.Ordinal);

  private static IEnumerable<Base> GetBaseList(Base b, string key)
  {
    var members = b.GetMembers(DynamicBaseMemberType.Instance | DynamicBaseMemberType.Dynamic);
    // A detached list may live under the typed key OR the `@`-prefixed dynamic key (older connectors /
    // app.speckle.systems keep `@elements`/`@displayValue` as dynamic members the deserializer didn't map
    // onto the typed property). Take whichever is a non-empty Base list.
    var raw = NonEmpty(members.GetValueOrDefault(key)) ?? NonEmpty(members.GetValueOrDefault("@" + key));
    if (raw is System.Collections.IEnumerable seq and not string)
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

  // Returns the value only if it's a non-empty enumerable (so an empty typed `elements` falls through to
  // the `@elements` dynamic member).
  private static object? NonEmpty(object? v) =>
    v is System.Collections.ICollection c && c.Count == 0 ? null : v;

  private static IEnumerable<string> AsStringList(object? raw)
  {
    if (raw is System.Collections.IEnumerable seq and not string)
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
      m.M11, m.M12, m.M13, m.M14,
      m.M21, m.M22, m.M23, m.M24,
      m.M31, m.M32, m.M33, m.M34,
      m.M41, m.M42, m.M43, m.M44,
    };
}
