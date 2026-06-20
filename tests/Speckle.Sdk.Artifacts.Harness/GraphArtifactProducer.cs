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

      var objK = EmitObject(pipeline, obj, stats, seenObjectAppIds, seenGeometryAppIds, instanceNodeByAppId);

      // host→hosted nesting: a non-collection object carrying `elements` (e.g. curtain wall → panels).
      foreach (var child in GetBaseList(obj, "elements"))
      {
        if (child is Collection || defSourceAppIds.Contains(Aid(child)))
        {
          continue;
        }
        var childK = EmitObject(pipeline, child, stats, seenObjectAppIds, seenGeometryAppIds, instanceNodeByAppId);
        pipeline.Subelement(objK, childK, stats.SubelementEdges++);
      }
      _ = parent;
    }

    // 2) Value-nodes + their edges, from the root collection's proxy arrays.
    EmitProxies(pipeline, root, stats, seenObjectAppIds, seenGeometryAppIds, instanceNodeByAppId);

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
    Dictionary<string, int> instanceNodeByAppId
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
    switch (obj)
    {
      case InstanceProxy ip:
        // instance-atomic: the leaf IS a placement → object DISPLAY_INSTANCEs its own INSTANCE node.
        var instK = ResolveInstanceNode(pipeline, ip, instanceNodeByAppId);
        pipeline.DisplayInstance(objK, instK, 0);
        stats.DisplayInstanceEdges++;
        stats.InstanceAtomics++;
        break;

      case { } g when IsGeometry(g):
        // mesh-atomic: the leaf IS the geometry → DISPLAY(object-K → its own geometry-K). The appId
        // interns into BOTH the object and geometry namespaces (separate counters, no collision).
        if (TryAddGeometry(pipeline, appId, g, stats, seenGeometryAppIds))
        {
          pipeline.Display(objK, pipeline.InternGeometryId(appId), 0);
          stats.DisplayEdges++;
          stats.MeshAtomics++;
        }
        break;

      default:
        // dataobject-atomic: references its geometry/instances via displayValue (as today).
        int ord = 0;
        foreach (var item in GetBaseList(obj, "displayValue"))
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
            }
          }
        }
        break;
    }

    return objK;
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
    var geometry = IsGeometry(obj)
      ? obj
      : GetBaseList(obj, "displayValue").FirstOrDefault(d => d is not InstanceProxy);
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
    Dictionary<string, int> instanceNodeByAppId
  )
  {
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

    // Materials: HAS_MATERIAL(mesh → material), per-mesh (SOT Fact 2). objects[] are mesh appIds.
    foreach (var mat in GetBaseList(root, "renderMaterialProxies"))
    {
      if (mat is not RenderMaterialProxy rmp)
      {
        continue;
      }
      var v = rmp.value;
      var matK = pipeline.AddMaterial(MaterialKey(rmp), v.diffuse, v.opacity, v.metalness, v.roughness);
      stats.Materials++;
      foreach (var meshAppId in rmp.objects)
      {
        // Skip if the mesh isn't in geometries — emitting would mint a phantom geometry K.
        if (!seenGeometryAppIds.Contains(meshAppId))
        {
          stats.SkippedMaterial++;
          continue;
        }
        pipeline.HasMaterial(pipeline.InternGeometryId(meshAppId), matK);
        stats.HasMaterialEdges++;
      }
    }

    // Colors: HAS_COLOR(object|mesh → color). Resolve to whichever namespace the ref lives in.
    foreach (var col in GetBaseList(root, "colorProxies"))
    {
      if (col is not ColorProxy cp)
      {
        continue;
      }
      var colK = pipeline.AddColor(cp.value);
      stats.Colors++;
      foreach (var refAppId in cp.objects)
      {
        if (seenObjectAppIds.Contains(refAppId))
        {
          pipeline.HasColor(pipeline.InternObject(refAppId), colK);
          stats.HasColorEdges++;
        }
        else if (seenGeometryAppIds.Contains(refAppId))
        {
          pipeline.HasColor(pipeline.InternGeometryId(refAppId), colK);
          stats.HasColorEdges++;
        }
        else
        {
          stats.SkippedColor++;
        }
      }
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

  // Yields every collection LEAF reachable through `elements`, with its parent. A leaf is atomic
  // type-agnostically (SOT §1) — DataObject, raw geometry, or a bare InstanceProxy placement. Only
  // containers (Collection) and the value/definition proxy arrays (which live on dynamic members, never
  // in `elements`) are excluded; a bare InstanceProxy IS a yielded atomic leaf.
  private static IEnumerable<(Base obj, Base? parent)> TraverseAtomics(Base root)
  {
    var stack = new Stack<(Base node, Base? parent)>();
    stack.Push((root, null));
    while (stack.Count > 0)
    {
      var (node, parent) = stack.Pop();
      var isContainer = node is Collection || ReferenceEquals(node, root);
      if (!isContainer && !IsValueOrDefinitionProxy(node))
      {
        yield return (node, parent);
      }
      foreach (var child in GetBaseList(node, "elements"))
      {
        stack.Push((child, node));
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
