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
      objects={Objects}  geometries={Geometries} (defGeom={DefinitionGeometries})  encodeFailures={GeometryEncodeFailures}
      edges: DISPLAY={DisplayEdges} DISPLAY_INSTANCE={DisplayInstanceEdges} SUBELEMENT={SubelementEdges}
             DEFINES={DefinesEdges} HAS_MATERIAL={HasMaterialEdges} HAS_COLOR={HasColorEdges} ON_LEVEL={OnLevelEdges}
      nodes: DEFINITION={Definitions} MATERIAL={Materials} COLOR={Colors} LEVEL={Levels}
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

    // Instance-definition source geometry: meshes referenced by `instanceDefinitionProxies[].objects[]`.
    // These live standalone in the `elements` tree (old-connector style) but are NOT atomic objects — they
    // are the definition's raw shape sources (rendered via instance placements, never directly). Route them
    // to the GEOMETRY namespace so DEFINES + HAS_MATERIAL resolve to real SGEO blobs (mirrors ODA's
    // DefinitionGeometries). Without this they fall into the object namespace and every DEFINES/HAS_MATERIAL
    // edge that targets them dangles. See notes/server-v2-migration-plan-SOT.md.
    var defSourceAppIds = CollectDefinitionSourceAppIds(root);

    // 1) Walk the object tree. Definition-source meshes → geometry; everything else → atomic object.
    foreach (var (obj, parent) in TraverseAtomics(root))
    {
      if (defSourceAppIds.Contains(Aid(obj)))
      {
        EmitDefinitionGeometry(pipeline, obj, stats, seenGeometryAppIds);
        continue;
      }

      var objK = EmitObject(pipeline, obj, stats, seenObjectAppIds, seenGeometryAppIds);

      // host→hosted nesting: a non-collection object carrying `elements` (e.g. curtain wall → panels).
      foreach (var child in GetBaseList(obj, "elements"))
      {
        if (child is Collection || defSourceAppIds.Contains(Aid(child)))
        {
          continue;
        }
        var childK = EmitObject(pipeline, child, stats, seenObjectAppIds, seenGeometryAppIds);
        pipeline.Subelement(objK, childK, stats.SubelementEdges++);
      }
      _ = parent;
    }

    // 2) Value-nodes + their edges, from the root collection's proxy arrays.
    EmitProxies(pipeline, root, stats, seenObjectAppIds, seenGeometryAppIds);

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
    HashSet<string> seenGeometryAppIds
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

    int ord = 0;
    foreach (var item in GetBaseList(obj, "displayValue"))
    {
      switch (item)
      {
        case InstanceProxy ip:
          var defK = pipeline.AddDefinition(ip.definitionId, ip.definitionId);
          var instK = pipeline.AddInstance(InstanceKey(ip), defK, Flatten(ip.transform), ip.units);
          pipeline.DisplayInstance(objK, instK, ord++);
          stats.DisplayInstanceEdges++;
          break;
        default:
          var gAppId = GeometryKey(item);
          try
          {
            var gK = pipeline.AddGeometry(gAppId, item);
            seenGeometryAppIds.Add(gAppId);
            pipeline.Display(objK, gK, ord++);
            stats.DisplayEdges++;
            stats.Geometries = Math.Max(stats.Geometries, seenGeometryAppIds.Count);
          }
          catch (Exception ex)
          {
            stats.GeometryEncodeFailures++;
            if (stats.Notes.Count < 20)
            {
              stats.Notes.Add($"encode fail [{item.speckle_type}]: {ex.Message}");
            }
          }
          break;
      }
    }

    return objK;
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

  // Encode a definition source as a geometry blob under its OWN applicationId (so DEFINES/HAS_MATERIAL
  // edges that reference it resolve). A bare geometry (Mesh/Brep/…) is encoded directly; a display-bearing
  // object contributes its first non-instance displayValue mesh under the object's appId.
  private static void EmitDefinitionGeometry(
    ObjectsArtifactPipeline pipeline,
    Base obj,
    Stats stats,
    HashSet<string> seenGeometryAppIds
  )
  {
    var appId = Aid(obj);
    if (!seenGeometryAppIds.Add(appId))
    {
      return;
    }
    var geometry = IsGeometry(obj)
      ? obj
      : GetBaseList(obj, "displayValue").FirstOrDefault(d => d is not InstanceProxy);
    if (geometry is null)
    {
      stats.SkippedDefines++;
      if (stats.Notes.Count < 20)
      {
        stats.Notes.Add($"def source {appId} has no encodable geometry [{obj.speckle_type}]");
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
    HashSet<string> seenGeometryAppIds
  )
  {
    // Definitions: DEFINES(definition → member geometry). members are source-app appIds of meshes.
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
        // Skip if the member geometry isn't in the bundle — emitting would mint a phantom geometry K.
        if (!seenGeometryAppIds.Contains(memberAppId))
        {
          stats.SkippedDefines++;
          continue;
        }
        pipeline.Defines(defK, pipeline.InternGeometryId(memberAppId), o++);
        stats.DefinesEdges++;
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

  // Yields every atomic object (non-collection, non-proxy) reachable through `elements`, with its parent.
  private static IEnumerable<(Base obj, Base? parent)> TraverseAtomics(Base root)
  {
    var stack = new Stack<(Base node, Base? parent)>();
    stack.Push((root, null));
    while (stack.Count > 0)
    {
      var (node, parent) = stack.Pop();
      var isContainer = node is Collection || ReferenceEquals(node, root);
      if (!isContainer && !IsProxy(node))
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

  private static string InstanceKey(InstanceProxy ip) => ip.applicationId ?? "spk:" + ip.id;

  private static string DefinitionKey(InstanceDefinitionProxy idp) => idp.applicationId ?? idp.name;

  private static string MaterialKey(RenderMaterialProxy rmp) =>
    rmp.applicationId ?? rmp.value.applicationId ?? "mat:" + rmp.value.diffuse.ToString(CultureInfo.InvariantCulture);

  private static string LevelKey(Base lvl, string? name) => lvl.applicationId ?? "lvl:" + (name ?? lvl.id);

  // ── helpers ──────────────────────────────────────────────────────────────────────

  private static bool IsProxy(Base b) =>
    b is InstanceDefinitionProxy or InstanceProxy or RenderMaterialProxy or ColorProxy or GroupProxy;

  private static bool IsGeometry(Base b) =>
    b.speckle_type.StartsWith("Objects.Geometry.", StringComparison.Ordinal);

  private static IEnumerable<Base> GetBaseList(Base b, string key)
  {
    var members = b.GetMembers(DynamicBaseMemberType.Instance | DynamicBaseMemberType.Dynamic);
    if (!members.TryGetValue(key, out var raw) || raw is null)
    {
      yield break;
    }
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
