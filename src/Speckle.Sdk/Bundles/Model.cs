using Microsoft.Extensions.Logging;
using Speckle.Sdk.Pipelines;
using Speckle.Sdk.Pipelines.Receive.Artifacts;

namespace Speckle.Sdk.Bundles;

/// <summary>
/// One received version of a model, as delivered by <see cref="Api.Operations.Receive3(Uri, string, string, string, string?, ReceiveOptions?, CancellationToken)"/> — the Speckle 2026.9.0 read
/// surface. Wraps the version's artefact bundle and owns the local bundle files until disposed.
/// </summary>
/// <remarks>
/// Start from <see cref="Objects"/> and query with LINQ; follow relationships through the accessors on
/// <see cref="ModelObject"/> / <see cref="ModelNode"/> / <see cref="ModelGeometry"/>; drop to <see cref="Bundle"/> for
/// the raw dense-int graph. The bundle parquet files stay on disk under <see cref="Directory"/> for the lifetime of
/// this instance so other consumers (a SQL engine, a host bake) can read them without re-downloading. Dispose to
/// delete them.
/// <para>Name clash: <c>Speckle.Sdk.Api.GraphQL.Models.Model</c> is the GraphQL model record. In a file that imports
/// both namespaces, disambiguate with <c>using Model = Speckle.Sdk.Bundles.Model;</c>.</para>
/// </remarks>
public sealed class Model : IDisposable
{
  private readonly ILogger _logger;
  private readonly bool _geometryDownloaded;
  private readonly Lazy<IReadOnlyList<ModelObject>> _objects;
  private readonly Lazy<Dictionary<int, ModelObject>> _objectByK;
  private readonly Lazy<Dictionary<string, ModelObject>> _objectByAppId;
  private readonly Lazy<Dictionary<int, ModelNode>> _nodes;
  private readonly Lazy<IReadOnlyDictionary<int, ArtefactGeometry>> _geometries;
  private readonly Lazy<RelationIndex> _index;
  private readonly Lazy<IReadOnlyList<ModelSceneViewTier>> _sceneView;
  private readonly Lazy<IReadOnlyDictionary<string, object?>> _modelProperties;
  private bool _disposed;

  internal Model(
    string projectId,
    string modelId,
    string versionId,
    string directory,
    IReadOnlyList<string> files,
    ArtefactBundle bundle,
    bool geometryDownloaded,
    ILogger logger
  )
  {
    ProjectId = projectId;
    ModelId = modelId;
    VersionId = versionId;
    Directory = directory;
    Files = files;
    Bundle = bundle;
    _geometryDownloaded = geometryDownloaded;
    _logger = logger;
    _objects = new(BuildObjects);
    _objectByK = new(() => _objects.Value.ToDictionary(o => o.K));
    _objectByAppId = new(BuildObjectsByAppId);
    _nodes = new(() => Bundle.Nodes.ToDictionary(kv => kv.Key, kv => ModelNode.Create(this, kv.Key, kv.Value)));
    _geometries = new(LoadGeometries);
    _index = new(() => new RelationIndex(Bundle));
    _sceneView = new(() => Bundle.DefaultSceneView.Select(t => new ModelSceneViewTier(t)).ToList());
    _modelProperties = new(() => Flatten(Bundle.ModelProperties));
    if (bundle.Relations.UnknownRels.Count > 0)
    {
      logger.LogWarning(
        "Bundle {versionId} uses relation numbers this SDK does not know ({rels}); their edges were dropped. Upgrade Speckle.Sdk.",
        versionId,
        string.Join(",", bundle.Relations.UnknownRels.OrderBy(r => r))
      );
    }
  }

  public string ProjectId { get; }
  public string ModelId { get; }
  public string VersionId { get; }

  /// <summary>Local directory holding the bundle's parquet files. Valid until <see cref="Dispose"/>.</summary>
  public string Directory { get; }

  /// <summary>Full paths of the bundle's parquet files.</summary>
  public IReadOnlyList<string> Files { get; }

  /// <summary>Length unit every geometry in the bundle is expressed in (e.g. <c>"m"</c>, <c>"mm"</c>).</summary>
  public string Units => Bundle.Units;

  /// <summary>Model-level properties (<c>eav.model</c>: project information, placement transform, document settings),
  /// flat path-keyed like object properties.</summary>
  public IReadOnlyDictionary<string, object?> Properties => _modelProperties.Value;

  /// <summary>The default scene view's grouping tiers, outermost first — how the viewer builds its tree. Empty when the
  /// producer declared none (the scene tree is then the <c>IN_COLLECTION</c> hierarchy).</summary>
  public IReadOnlyList<ModelSceneViewTier> DefaultSceneView => _sceneView.Value;

  /// <summary>Named camera viewpoints (<c>envelope.camera_views</c>); empty if the bundle ships none.</summary>
  public IReadOnlyList<ArtefactCameraView> CameraViews => Bundle.CameraViews;

  /// <summary>AEC property-set definitions (<c>eav.property_set_definitions</c>), one per (set, field); empty if absent.</summary>
  public IReadOnlyList<ArtefactPropertySetField> PropertySetDefinitions => Bundle.PropertySetDefinitions;

  /// <summary>Relation numbers in the bundle this SDK doesn't know (a newer bundle spec than the SDK was built against).
  /// Their edges were dropped; empty when the vocabularies match.</summary>
  public IReadOnlyCollection<int> UnknownRelations => Bundle.Relations.UnknownRels;

  // ── objects ───────────────────────────────────────────────────────────────────────────────────────────

  /// <summary>Every object in the version, in bundle order. Objects are the property-carrying entities (walls, pipes,
  /// blocks, …); levels, materials, definitions and containers are <see cref="Nodes"/>.</summary>
  public IReadOnlyList<ModelObject> Objects => _objects.Value;

  /// <summary>Object by dense index, or null.</summary>
  public ModelObject? Object(int k) => _objectByK.Value.TryGetValue(k, out var o) ? o : null;

  /// <summary>Every distinct instance-property path in the model (without the <c>properties.</c> root) — what
  /// <see cref="ModelObject.Properties"/> keys can be. Read from the interned path table; touches no object.</summary>
  public IReadOnlyList<string> PropertyPaths =>
    Properties_
      .Paths.Where(p => p.StartsWith(ModelObject.PROPERTIES_PREFIX, StringComparison.Ordinal))
      .Select(p => p.Substring(ModelObject.PROPERTIES_PREFIX.Length))
      .ToList();

  /// <summary>Objects carrying an instance property under <paramref name="path"/> — one scan of the path column,
  /// no per-object work. Pair with <see cref="ModelObject.GetDouble"/> etc. for the value.</summary>
  public IEnumerable<ModelObject> ObjectsWith(string path)
  {
    foreach (int k in Properties_.KeysWith(ModelObject.PROPERTIES_PREFIX + path))
    {
      if (Object(k) is { } o)
      {
        yield return o;
      }
    }
  }

  internal PropertyTable Properties_ =>
    Bundle.PropertyTable
    ?? throw new InvalidOperationException("Model requires a bundle read with ArtefactReadOptions.ColumnarProperties.");

  internal PropertyTable TypeProperties_ => Bundle.TypePropertyTable ?? PropertyTable.Empty;

  /// <summary>Object by host application id, or null.</summary>
  public ModelObject? ObjectByApplicationId(string applicationId) =>
    _objectByAppId.Value.TryGetValue(applicationId, out var o) ? o : null;

  // ── nodes ─────────────────────────────────────────────────────────────────────────────────────────────

  /// <summary>Every envelope node keyed by dense index.</summary>
  public IReadOnlyDictionary<int, ModelNode> Nodes => _nodes.Value;

  /// <summary>Node by dense index, or null.</summary>
  public ModelNode? Node(int k) => _nodes.Value.TryGetValue(k, out var n) ? n : null;

  public IReadOnlyList<ModelLevel> Levels => NodesOfType<ModelLevel>();
  public IReadOnlyList<ModelMaterial> Materials => NodesOfType<ModelMaterial>();
  public IReadOnlyList<ModelColor> Colors => NodesOfType<ModelColor>();
  public IReadOnlyList<ModelDefinition> Definitions => NodesOfType<ModelDefinition>();

  /// <summary>Scene-tree containers (layers, categories, groups, models …).</summary>
  public IReadOnlyList<ModelContainer> Collections => NodesOfType<ModelContainer>();

  private IReadOnlyList<T> NodesOfType<T>()
    where T : ModelNode => _nodes.Value.Values.OfType<T>().OrderBy(n => n.K).ToList();

  // ── geometry ──────────────────────────────────────────────────────────────────────────────────────────

  /// <summary>
  /// Raw geometry blobs keyed by dense geometry index. Parsed from the downloaded shards on first access — geometry is
  /// the bulk of a bundle, so it stays on disk until something asks for it (this, or any <see cref="ModelObject.Geometries"/>).
  /// Requires the files to still exist: access before <see cref="Dispose"/>.
  /// </summary>
  /// <exception cref="InvalidOperationException">Received with <see cref="ReceiveOptions.IncludeGeometry"/> false, or
  /// already disposed.</exception>
  public IReadOnlyDictionary<int, ArtefactGeometry> Geometries => _geometries.Value;

  /// <summary>Whether <see cref="Geometries"/> has been parsed yet.</summary>
  public bool IsGeometryLoaded => _geometries.IsValueCreated;

  /// <summary>The parsed bundle: dense-int object / geometry / node ids, relations, scene views. NB
  /// <see cref="ArtefactBundle.Geometries"/> on this instance is empty — geometry is deferred; use
  /// <see cref="Geometries"/>.</summary>
  public ArtefactBundle Bundle { get; }

  // ── internals shared by the façade types ──────────────────────────────────────────────────────────────

  internal RelationIndex Index => _index.Value;

  internal ModelNode? NodeOrNull(int? k) => k is int key && _nodes.Value.TryGetValue(key, out var n) ? n : null;

  internal IReadOnlyList<ModelObject> ObjectsFor(IReadOnlyList<int>? ks)
  {
    if (ks is null || ks.Count == 0)
    {
      return Array.Empty<ModelObject>();
    }
    var list = new List<ModelObject>(ks.Count);
    foreach (int k in ks)
    {
      if (_objectByK.Value.TryGetValue(k, out var o))
      {
        list.Add(o);
      }
    }
    return list;
  }

  internal IReadOnlyList<T> NodesFor<T>(IReadOnlyList<int>? ks)
    where T : ModelNode
  {
    if (ks is null || ks.Count == 0)
    {
      return Array.Empty<T>();
    }
    var list = new List<T>(ks.Count);
    foreach (int k in ks)
    {
      if (_nodes.Value.TryGetValue(k, out var n) && n is T typed)
      {
        list.Add(typed);
      }
    }
    return list;
  }

  internal IReadOnlyList<ModelObject> ObjectsInCollection(int nodeK) =>
    ObjectsFor(Index.Get(Index.ObjectsByCollection, nodeK));

  internal IReadOnlyList<ModelObject> ObjectsOnLevel(int levelK) => ObjectsFor(Index.Get(Index.ObjectsByLevel, levelK));

  internal IReadOnlyList<ModelContainer> ChildContainers(int containerK) =>
    NodesFor<ModelContainer>(Index.Get(Index.ChildContainersByContainer, containerK));

  internal IReadOnlyList<ModelInstance> PlacementsOfDefinition(int definitionK) =>
    NodesFor<ModelInstance>(Index.Get(Index.InstancesByDefinition, definitionK));

  internal IReadOnlyList<ModelObject> ObjectsOfDefinition(int definitionK) =>
    ObjectsFor(Index.Get(Index.ObjectsByDefinition, definitionK));

  internal IReadOnlyList<ModelObject> MembersOfDefinition(int definitionK) =>
    ObjectsFor(Bundle.Relations.MemberObjectsByDefinition.TryGetValue(definitionK, out var m) ? m : null);

  // The reader nests model-scoped rows by dotted path; the façade addresses them flat, like everything else.
  private static IReadOnlyDictionary<string, object?> Flatten(IReadOnlyDictionary<string, object?> nested)
  {
    var flat = new Dictionary<string, object?>(StringComparer.Ordinal);
    Walk(nested, null);
    return flat;

    void Walk(IReadOnlyDictionary<string, object?> dict, string? prefix)
    {
      foreach (var kv in dict)
      {
        string path = prefix is null ? kv.Key : prefix + "." + kv.Key;
        if (kv.Value is IReadOnlyDictionary<string, object?> child)
        {
          Walk(child, path);
        }
        else
        {
          flat[path] = kv.Value;
        }
      }
    }
  }

  private IReadOnlyList<ModelObject> BuildObjects()
  {
    var list = new List<ModelObject>(Bundle.ObjectAppIds.Count);
    foreach (var kv in Bundle.ObjectAppIds.OrderBy(kv => kv.Key))
    {
      list.Add(new ModelObject(this, kv.Key, kv.Value));
    }
    return list;
  }

  private Dictionary<string, ModelObject> BuildObjectsByAppId()
  {
    var map = new Dictionary<string, ModelObject>(StringComparer.Ordinal);
    foreach (var o in _objects.Value)
    {
      map[o.ApplicationId] = o; // last wins on the (invalid) duplicate case
    }
    return map;
  }

  private IReadOnlyDictionary<int, ArtefactGeometry> LoadGeometries()
  {
    if (!_geometryDownloaded)
    {
      throw new InvalidOperationException(
        "This model was received with ReceiveOptions.IncludeGeometry = false; no geometry is available. Receive it "
          + "again with the default options to access geometry."
      );
    }
    if (_disposed)
    {
      throw new InvalidOperationException(
        "Geometry is parsed from the bundle files on first access, and this model has been disposed (files deleted). "
          + "Access Model.Geometries before disposing."
      );
    }
    return ArtefactBundleReader.ReadGeometriesAsync(Directory, CancellationToken.None).GetAwaiter().GetResult();
  }

  /// <summary>Deletes the local bundle files. <see cref="Bundle"/>, <see cref="Objects"/> and an already-loaded
  /// <see cref="Geometries"/> stay usable (in memory); an unloaded <see cref="Geometries"/> can no longer be read.</summary>
  public void Dispose()
  {
    if (_disposed)
    {
      return;
    }
    _disposed = true;
    try
    {
      if (System.IO.Directory.Exists(Directory))
      {
        System.IO.Directory.Delete(Directory, true);
      }
    }
    catch (IOException ex)
    {
      _logger.LogWarning(ex, "Could not delete bundle directory {directory}", Directory);
    }
    catch (UnauthorizedAccessException ex)
    {
      _logger.LogWarning(ex, "Could not delete bundle directory {directory}", Directory);
    }
  }
}

/// <summary>
/// Reverse / grouped views over <see cref="ArtefactRelations"/> that the reader doesn't keep (it groups by source
/// only). Built once per model on first relationship access.
/// </summary>
internal sealed class RelationIndex
{
  public Dictionary<int, List<int>> ChildrenByParent { get; } = new(); // SUBELEMENT
  public Dictionary<int, int> ParentByChild { get; } = new();
  public Dictionary<int, List<int>> HostedByHost { get; } = new(); // HOSTED_ON
  public Dictionary<int, int> HostByHosted { get; } = new();
  public Dictionary<int, List<int>> Connections { get; } = new(); // CONNECTS_TO, both directions
  public Dictionary<int, List<int>> RoomsByBounding { get; } = new(); // BOUNDS
  public Dictionary<int, List<int>> BoundingByRoom { get; } = new();
  public Dictionary<int, int> RoomByObject { get; } = new(); // IN_ROOM
  public Dictionary<int, List<int>> ObjectsByRoom { get; } = new();
  public Dictionary<int, int> AssemblyByMember { get; } = new(); // IN_ASSEMBLY
  public Dictionary<int, List<int>> MembersByAssembly { get; } = new();
  public Dictionary<int, List<int>> InstancesByObject { get; } = new(); // DISPLAY_INSTANCE, all edges
  public Dictionary<int, List<int>> InstancesByDefinition { get; } = new(); // INSTANCE nodes by DefRef
  public Dictionary<int, List<int>> ObjectsByDefinition { get; } = new(); // DISPLAY_INSTANCE → INSTANCE.DefRef, reversed
  public Dictionary<int, List<int>> ObjectsByCollection { get; } = new(); // IN_COLLECTION reversed
  public Dictionary<int, List<int>> ObjectsByLevel { get; } = new(); // ON_LEVEL reversed
  public Dictionary<int, List<int>> ChildContainersByContainer { get; } = new(); // CONTAINER.def_ref reversed

  public RelationIndex(ArtefactBundle bundle)
  {
    var rels = bundle.Relations;
    foreach (var e in rels.Subelement.OrderBy(e => e.Ord))
    {
      Add(ChildrenByParent, e.Src, e.Dst);
      ParentByChild[e.Dst] = e.Src;
    }
    foreach (var e in rels.HostedOn)
    {
      Add(HostedByHost, e.Dst, e.Src);
      HostByHosted[e.Src] = e.Dst;
    }
    foreach (var e in rels.ConnectsTo)
    {
      Add(Connections, e.Src, e.Dst);
      Add(Connections, e.Dst, e.Src);
    }
    foreach (var e in rels.Bounds.OrderBy(e => e.Ord))
    {
      Add(RoomsByBounding, e.Src, e.Dst);
      Add(BoundingByRoom, e.Dst, e.Src);
    }
    foreach (var e in rels.InRoom)
    {
      RoomByObject[e.Src] = e.Dst;
      Add(ObjectsByRoom, e.Dst, e.Src);
    }
    foreach (var e in rels.InAssembly.OrderBy(e => e.Ord))
    {
      AssemblyByMember[e.Src] = e.Dst;
      Add(MembersByAssembly, e.Dst, e.Src);
    }
    foreach (var e in rels.DisplayInstanceEdges.OrderBy(e => e.Ord))
    {
      Add(InstancesByObject, e.Src, e.Dst);
      if (bundle.Nodes.TryGetValue(e.Dst, out var inst) && inst.DefRef is int defK)
      {
        if (!ObjectsByDefinition.TryGetValue(defK, out var objs) || !objs.Contains(e.Src))
        {
          Add(ObjectsByDefinition, defK, e.Src);
        }
      }
    }
    foreach (var kv in bundle.Nodes)
    {
      if (kv.Value.DefRef is not int def)
      {
        continue;
      }
      if (kv.Value.Kind == NodeKind.Instance)
      {
        Add(InstancesByDefinition, def, kv.Key);
      }
      else if (kv.Value.Kind == NodeKind.Container)
      {
        Add(ChildContainersByContainer, def, kv.Key);
      }
    }
    if (rels.ObjectNodeByRel.TryGetValue(RelKind.OnLevel, out var byLevel))
    {
      foreach (var kv in byLevel)
      {
        Add(ObjectsByLevel, kv.Value, kv.Key);
      }
    }
    foreach (var kv in rels.CollectionByObject)
    {
      Add(ObjectsByCollection, kv.Value, kv.Key);
    }
  }

  public IReadOnlyList<int>? Get(Dictionary<int, List<int>> map, int k) => map.TryGetValue(k, out var l) ? l : null;

  private static void Add(Dictionary<int, List<int>> map, int key, int value)
  {
    if (!map.TryGetValue(key, out var list))
    {
      map[key] = list = new List<int>();
    }
    list.Add(value);
  }
}
