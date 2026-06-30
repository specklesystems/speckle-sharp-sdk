#if NETSTANDARD2_0 || NET8_0_OR_GREATER
using System.Globalization;
using Speckle.Sdk.Pipelines.Send.Artifacts;

namespace Speckle.Sdk.Pipelines.Receive.Artifacts;

/// <summary>A single decoded geometry payload from the <c>geometries</c> parquet: the raw blob plus its type tag
/// (<c>"SGEO"</c>-prefixed mesh blob, or a raw encoding like <c>rhino/3dm</c>).</summary>
#pragma warning disable CA1819 // raw geometry blob is intentionally a byte[]; this is a lightweight transport record
public readonly record struct ArtefactGeometry(byte[] Content, string Type)
#pragma warning restore CA1819
{
  /// <summary>True when the blob is a SGEO mesh blob (decode with <c>SgeoDecoder</c>), false for raw encodings (3dm).</summary>
  public bool IsSgeo =>
    Content.Length >= 4 && Content[0] == (byte)'S' && Content[1] == (byte)'G' && Content[2] == (byte)'E' && Content[3] == (byte)'O';
}

/// <summary>One grouping tier of a scene view (the explorer/layer hierarchy). <see cref="Source"/> is <c>"rel"</c>
/// (group by a relation's target node, <see cref="Ref"/> = the <c>RelKind</c> number, e.g. 11=IN_MODEL, 7=ON_LEVEL,
/// 10=IN_COLLECTION) or <c>"eav"</c> (group by an object property, <see cref="Ref"/> = the property path, e.g.
/// <c>"category"</c>, <c>"family"</c>). Tiers are ordered outermost→innermost.</summary>
public readonly record struct SceneViewTier(string Source, string Ref);

/// <summary>An envelope graph node (Collection/Material/Definition/Instance/Level/…). Columns are sparse — only the
/// fields relevant to the node's <see cref="Kind"/> are populated.</summary>
public sealed record ArtefactNode(
  int Kind,
  string? Name,
  int? DefRef,
  string? Transform,
  string? Units,
  int? Argb,
  double? Opacity,
  double? Metalness,
  double? Roughness,
  double? Elevation
);

/// <summary>A relation edge in the envelope graph (<c>rel</c> = <see cref="RelKind"/>, <c>src</c>/<c>dst</c> dense ints).</summary>
public readonly record struct ArtefactEdge(int Src, int Dst, int Ord);

/// <summary>Envelope relations grouped by kind for direct lookup. The three dense-int namespaces are object
/// (<c>eav.object_index</c>), geometry (<c>geometryIndex</c>) and node (<c>nodes.id</c>); each relation maps between
/// two of them (e.g. DISPLAY src=object dst=geometry; IN_COLLECTION src=object dst=node).</summary>
public sealed class ArtefactRelations
{
  public List<ArtefactEdge> Display { get; } = new();
  public Dictionary<int, List<int>> SolidByObject { get; } = new();
  public Dictionary<int, int> CollectionByObject { get; } = new();

  /// <summary>DISPLAY_INSTANCE: object → INSTANCE node. Last-wins map (kept for the Base reconstruction path).</summary>
  public Dictionary<int, int> DisplayInstanceByObject { get; } = new();

  /// <summary>All DISPLAY_INSTANCE edges (object → INSTANCE node). An object may place several instances (e.g. a Revit
  /// railing → many balusters), so the native baker iterates these rather than the last-wins map above.</summary>
  public List<ArtefactEdge> DisplayInstanceEdges { get; } = new();

  /// <summary>For object→node relations (ON_LEVEL=7, IN_COLLECTION=10, IN_MODEL=11, IN_ROOM=12, …): rel → (object →
  /// target node). Used to resolve scene-view grouping tiers (e.g. an object's level/model/container) to a layer path.</summary>
  public Dictionary<int, Dictionary<int, int>> ObjectNodeByRel { get; } = new();
  public Dictionary<int, int> MaterialByGeometry { get; } = new();
  public Dictionary<int, List<int>> DefinesByDefinition { get; } = new();
  public Dictionary<int, List<int>> DefinesInstanceByDefinition { get; } = new();

  private Dictionary<int, List<ArtefactEdge>>? _displayByObject;

  /// <summary>The DISPLAY edges (object → mesh geometry) for one object, or null. Lazily indexed.</summary>
  public List<ArtefactEdge>? DisplayByObject(int objK)
  {
    _displayByObject ??= Display.GroupBy(e => e.Src).ToDictionary(g => g.Key, g => g.ToList());
    return _displayByObject.TryGetValue(objK, out var list) ? list : null;
  }

  /// <summary>Reverse of DISPLAY: mesh geometry index → owning object index (used to attribute HAS_MATERIAL/DEFINES,
  /// which target geometry, back to the owning object's applicationId).</summary>
  public Dictionary<int, int> ObjectByGeometry()
  {
    var map = new Dictionary<int, int>();
    foreach (var d in Display)
    {
      map[d.Dst] = d.Src;
    }
    return map;
  }

  internal void Add(Dictionary<int, List<int>> map, int key, int value)
  {
    if (!map.TryGetValue(key, out var list))
    {
      list = new List<int>();
      map[key] = list;
    }
    list.Add(value);
  }
}

/// <summary>
/// The neutral, host-agnostic parse of a Speckle 4.0 artefact bundle (the directory of
/// <c>geometries</c>/<c>eav.*</c>/<c>envelope.*</c> parquet files produced by the send pipeline). Holds the dense-int
/// graph + raw geometry blobs without committing to any reconstructed <c>Base</c> shape — consumed both by the Rhino
/// direct-bake host builder and by <c>ObjectsArtifactReader</c> (which maps it to a <c>Base</c>/<c>Collection</c> graph
/// for the connectors that still go through the v1 host-build path, e.g. Revit). Geometry blobs are kept raw; SGEO
/// decoding (which needs <c>Speckle.Objects</c>) happens in the consumer.
/// </summary>
public sealed class ArtefactBundle
{
  public required Dictionary<int, ArtefactGeometry> Geometries { get; init; }
  public required Dictionary<int, string> ObjectAppIds { get; init; }
  public required Dictionary<int, Dictionary<string, object?>> Properties { get; init; }
  public required Dictionary<int, ArtefactNode> Nodes { get; init; }
  public required ArtefactRelations Relations { get; init; }
  public required string Units { get; init; }

  /// <summary>The default scene view's grouping tiers (outermost→innermost), or empty if the bundle has none. Drives
  /// the received layer hierarchy (e.g. Revit: Model → Level → Category → Family).</summary>
  public required IReadOnlyList<SceneViewTier> DefaultSceneView { get; init; }
}

/// <summary>Reads the parquet files of an artefact bundle directory into a neutral <see cref="ArtefactBundle"/>.</summary>
public static class ArtefactBundleReader
{
  public static async Task<ArtefactBundle> ReadAsync(string bundleDir, CancellationToken cancellationToken)
  {
    var geometriesTables = await ReadShardsAsync(bundleDir, cancellationToken).ConfigureAwait(false);
    var objectsT = await ReadTableAsync(bundleDir, ".eav.objects.parquet", cancellationToken).ConfigureAwait(false);
    var pathsT = await ReadTableAsync(bundleDir, ".eav.paths.parquet", cancellationToken).ConfigureAwait(false);
    var eavT = await ReadTableAsync(bundleDir, ".eav.eav.parquet", cancellationToken).ConfigureAwait(false);
    var nodesT = await ReadTableAsync(bundleDir, ".envelope.nodes.parquet", cancellationToken).ConfigureAwait(false);
    var relationsT = await ReadTableAsync(bundleDir, ".envelope.relations.parquet", cancellationToken).ConfigureAwait(false);
    var sceneViewsT = await TryReadTableAsync(bundleDir, ".envelope.scene_views.parquet", cancellationToken).ConfigureAwait(false);

    var objIdToApp = BuildObjectIds(objectsT);
    var pathById = BuildPaths(pathsT);
    var propsByObject = BuildProperties(eavT, pathById);

    return new ArtefactBundle
    {
      Geometries = LoadGeometries(geometriesTables),
      ObjectAppIds = objIdToApp,
      Properties = propsByObject,
      Nodes = LoadNodes(nodesT),
      Relations = LoadRelations(relationsT),
      Units = InferUnits(propsByObject),
      DefaultSceneView = LoadDefaultSceneView(sceneViewsT),
    };
  }

  // object→node relations (per envelope rel_types) whose target node can form a scene-view grouping tier.
  private static readonly HashSet<int> s_objectNodeRels = new()
  {
    RelKind.OnLevel,
    RelKind.InCollection,
    RelKind.InModel,
    12, 13, 14, 15, 16, 17, 18, 19, 20, // IN_ROOM/IN_SPACE/IN_SYSTEM/IN_NETWORK/IN_LINE/IN_GROUP/IN_ASSEMBLY/…/XREF
  };

  private static IReadOnlyList<SceneViewTier> LoadDefaultSceneView(ParquetTable? t)
  {
    if (t is null || !t.Has("source"))
    {
      return Array.Empty<SceneViewTier>();
    }
    var isDefault = t.NullableBools("is_default");
    var ord = t.Ints("ord");
    var source = t.Strings("source");
    var refs = t.Strings("ref");
    var tiers = new List<(int Ord, SceneViewTier Tier)>();
    for (int i = 0; i < source.Length; i++)
    {
      if (isDefault[i] != true)
      {
        continue;
      }
      tiers.Add((ord[i], new SceneViewTier(source[i] ?? "", refs[i] ?? "")));
    }
    return tiers.OrderBy(x => x.Ord).Select(x => x.Tier).ToList();
  }

  private static Dictionary<int, ArtefactGeometry> LoadGeometries(List<ParquetTable> tables)
  {
    var map = new Dictionary<int, ArtefactGeometry>();
    foreach (var t in tables)
    {
      var idx = t.Ints("geometryIndex");
      var content = t.Blobs("content");
      var type = t.Strings("type");
      for (int i = 0; i < idx.Length; i++)
      {
        var bytes = content[i];
        if (bytes is null)
        {
          continue;
        }
        map[idx[i]] = new ArtefactGeometry(bytes, type[i] ?? "");
      }
    }
    return map;
  }

  private static Dictionary<int, string> BuildObjectIds(ParquetTable t)
  {
    var map = new Dictionary<int, string>();
    var idx = t.Ints("object_index");
    var app = t.Strings("application_id");
    for (int i = 0; i < idx.Length; i++)
    {
      map[idx[i]] = app[i] ?? idx[i].ToString(CultureInfo.InvariantCulture);
    }
    return map;
  }

  private static Dictionary<int, string> BuildPaths(ParquetTable t)
  {
    var map = new Dictionary<int, string>();
    var idx = t.Ints("path_index");
    var path = t.Strings("path");
    for (int i = 0; i < idx.Length; i++)
    {
      map[idx[i]] = path[i] ?? "";
    }
    return map;
  }

  private static Dictionary<int, Dictionary<string, object?>> BuildProperties(ParquetTable t, Dictionary<int, string> pathById)
  {
    var byObject = new Dictionary<int, Dictionary<string, object?>>();
    if (!t.Has("object_index"))
    {
      return byObject;
    }
    var objIdx = t.Ints("object_index");
    var pathIdx = t.Ints("path_index");
    var vStr = t.Strings("value_string");
    var vDbl = t.NullableDoubles("value_double");
    var vBool = t.NullableBools("value_boolean");
    for (int i = 0; i < objIdx.Length; i++)
    {
      object? value = vBool[i].HasValue ? vBool[i] : vDbl[i].HasValue ? vDbl[i] : vStr[i];
      if (value is null)
      {
        continue;
      }
      string path = pathById.TryGetValue(pathIdx[i], out var p) ? p : "";
      if (path.Length == 0)
      {
        continue;
      }
      if (!byObject.TryGetValue(objIdx[i], out var dict))
      {
        dict = new Dictionary<string, object?>();
        byObject[objIdx[i]] = dict;
      }
      SetNested(dict, path, value);
    }
    return byObject;
  }

  private static void SetNested(Dictionary<string, object?> root, string path, object? value)
  {
    var parts = path.Split('.');
    var cursor = root;
    for (int i = 0; i < parts.Length - 1; i++)
    {
      if (cursor.TryGetValue(parts[i], out var next) && next is Dictionary<string, object?> nd)
      {
        cursor = nd;
      }
      else
      {
        var nd2 = new Dictionary<string, object?>();
        cursor[parts[i]] = nd2;
        cursor = nd2;
      }
    }
    cursor[parts[^1]] = value;
  }

  private static string InferUnits(Dictionary<int, Dictionary<string, object?>> propsByObject)
  {
    foreach (var p in propsByObject.Values)
    {
      if (p.TryGetValue("units", out var v) && v is string s && s.Length > 0)
      {
        return s;
      }
    }
    return "none";
  }

  private static Dictionary<int, ArtefactNode> LoadNodes(ParquetTable t)
  {
    var map = new Dictionary<int, ArtefactNode>();
    var id = t.Ints("id");
    var kind = t.Ints("kind");
    var name = t.Strings("name");
    var defRef = t.NullableInts("def_ref");
    var transform = t.Strings("transform");
    var units = t.Strings("units");
    var argb = t.NullableInts("argb");
    var opacity = t.NullableDoubles("opacity");
    var metalness = t.NullableDoubles("metalness");
    var roughness = t.NullableDoubles("roughness");
    var elevation = t.NullableDoubles("elevation");
    for (int i = 0; i < id.Length; i++)
    {
      map[id[i]] = new ArtefactNode(
        kind[i],
        name[i],
        defRef[i],
        transform[i],
        units[i],
        argb[i],
        opacity[i],
        metalness[i],
        roughness[i],
        elevation[i]
      );
    }
    return map;
  }

  private static ArtefactRelations LoadRelations(ParquetTable t)
  {
    var sets = new ArtefactRelations();
    var rel = t.Ints("rel");
    var src = t.Ints("src");
    var dst = t.Ints("dst");
    var ord = t.Ints("ord");
    for (int i = 0; i < rel.Length; i++)
    {
      switch (rel[i])
      {
        case RelKind.Display:
          sets.Display.Add(new ArtefactEdge(src[i], dst[i], ord[i]));
          break;
        case RelKind.Solid:
          sets.Add(sets.SolidByObject, src[i], dst[i]);
          break;
        case RelKind.InCollection:
          sets.CollectionByObject[src[i]] = dst[i];
          break;
        case RelKind.DisplayInstance:
          sets.DisplayInstanceByObject[src[i]] = dst[i];
          sets.DisplayInstanceEdges.Add(new ArtefactEdge(src[i], dst[i], ord[i]));
          break;
        case RelKind.HasMaterial:
          sets.MaterialByGeometry[src[i]] = dst[i];
          break;
        case RelKind.Defines:
          sets.Add(sets.DefinesByDefinition, src[i], dst[i]);
          break;
        case RelKind.DefinesInstance:
          sets.Add(sets.DefinesInstanceByDefinition, src[i], dst[i]);
          break;
        default:
          break;
      }

      // object→node relations (ON_LEVEL/IN_COLLECTION/IN_MODEL/IN_ROOM/…) drive scene-view grouping tiers.
      if (s_objectNodeRels.Contains(rel[i]))
      {
        if (!sets.ObjectNodeByRel.TryGetValue(rel[i], out var map))
        {
          map = new Dictionary<int, int>();
          sets.ObjectNodeByRel[rel[i]] = map;
        }
        map[src[i]] = dst[i];
      }
    }
    return sets;
  }

  private static async Task<ParquetTable> ReadTableAsync(string dir, string suffix, CancellationToken ct)
  {
    var path =
      Directory.EnumerateFiles(dir, "*.parquet").FirstOrDefault(p => p.EndsWith(suffix, StringComparison.Ordinal))
      ?? throw new FileNotFoundException($"Artefact table '*{suffix}' not found in bundle '{dir}'.");
    return await ParquetTableReader.ReadAsync(path, ct).ConfigureAwait(false);
  }

  // Optional table (older bundles may lack e.g. scene_views) → null when absent.
  private static async Task<ParquetTable?> TryReadTableAsync(string dir, string suffix, CancellationToken ct)
  {
    var path = Directory.EnumerateFiles(dir, "*.parquet").FirstOrDefault(p => p.EndsWith(suffix, StringComparison.Ordinal));
    return path is null ? null : await ParquetTableReader.ReadAsync(path, ct).ConfigureAwait(false);
  }

  private static async Task<List<ParquetTable>> ReadShardsAsync(string dir, CancellationToken ct)
  {
    var tables = new List<ParquetTable>();
    foreach (var path in Directory.EnumerateFiles(dir, "*.geometries*.parquet").OrderBy(p => p, StringComparer.Ordinal))
    {
      tables.Add(await ParquetTableReader.ReadAsync(path, ct).ConfigureAwait(false));
    }
    if (tables.Count == 0)
    {
      throw new FileNotFoundException($"Artefact geometries '*.geometries*.parquet' not found in bundle '{dir}'.");
    }
    return tables;
  }
}
#endif
