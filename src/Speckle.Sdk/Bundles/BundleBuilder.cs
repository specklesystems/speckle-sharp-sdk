using System.Diagnostics.CodeAnalysis;
using Speckle.Objects.Utils;
using Speckle.Sdk.Pipelines;
using Speckle.Sdk.Pipelines.Send.Artifacts;

namespace Speckle.Sdk.Bundles;

/// <summary>
/// Writes a Speckle 2026.9.0 artefact bundle from typed handles — the write-side twin of <see cref="Model"/>. Wraps
/// <see cref="ObjectsArtifactPipeline"/> and owns everything the connectors' send builders used to re-implement:
/// dense-int allocation, edge ordinals, collection paths, the producer stamp, the default scene view, and the final
/// file listing. Everything streams to parquet as it is added (geometry is SGEO-encoded on the spot), so memory does
/// not grow with the model.
/// </summary>
/// <remarks>
/// Typical shape:
/// <code>
/// using var b = new BundleBuilder(app, units: "m");
/// var walls = b.GetOrAddContainerPath(["Level 1", "Walls"], subtype: "Category");
/// var wall  = b.GetOrAddObject("wall-1");
/// wall.SetProperties(properties, name: "Basic Wall", speckleType: "Objects.Data.DataObject");
/// wall.Collection = walls;
/// wall.AddGeometry(mesh).Material = b.GetOrAddMaterial("concrete", "Concrete", 0xFF808080, 1, 0, 0.8);
/// wall.Level = b.GetOrAddLevel("L1", "Level 1", 0);
/// var chair = b.GetOrAddObject("chair-1");
/// chair.Place(b.GetOrAddDefinition("def-chair", "Chair", d => d.AddGeometry(chairMesh)), transform, "m");
/// BundleFiles files = b.Build();
/// </code>
/// Naming rule: <c>GetOrAdd…</c> interns a node by key — the same key returns the same handle and writes nothing new,
/// and a repeat with different attributes throws (a key collision with different content would be a corrupt bundle).
/// <c>Add…</c> appends a row every call (geometry, model properties, results, camera views). Verbs and property
/// setters (<c>Place</c>, <c>ConnectTo</c>, <c>Host =</c>) emit one edge each. Edge ordinals follow call order.
/// </remarks>
public sealed class BundleBuilder : IDisposable
{
  private const string DEFAULT_BASE_NAME = "bundle";

  private readonly Dictionary<string, BundleObject> _objects = new(StringComparer.Ordinal);
  private readonly Dictionary<string, BundleContainer> _containers = new(StringComparer.Ordinal);
  private readonly Dictionary<string, BundleDefinition> _definitions = new(StringComparer.Ordinal);
  private readonly Dictionary<string, BundleMaterial> _materials = new(StringComparer.Ordinal);
  private readonly Dictionary<int, BundleColor> _colors = new();
  private readonly Dictionary<string, BundleLevel> _levels = new(StringComparer.Ordinal);
  private readonly Dictionary<string, BundleGeometry> _geometries = new(StringComparer.Ordinal);
  private readonly List<SceneView> _sceneViews = new();
  private bool _built;
  private bool _disposed;

  /// <param name="producer">Who is writing the bundle — stamped into <c>envelope.meta</c>. Required.</param>
  /// <param name="units">Model length unit (<c>"m"</c>, <c>"mm"</c>, <c>"ft"</c> …), written as each object's root <c>units</c>
  /// scalar unless overridden per object.</param>
  /// <param name="outputDir">Where the parquet files go; a fresh directory under the temp path when null.</param>
  /// <param name="baseName">File basename (<c>{baseName}.eav.parquet</c> …). Defaults to <c>"bundle"</c>;
  /// <see cref="BundleFiles.RenameTo"/> re-keys the files to a version id before upload.</param>
  public BundleBuilder(ISpeckleApplication producer, string units, string? outputDir = null, string? baseName = null)
  {
    Producer = producer ?? throw new ArgumentNullException(nameof(producer));
    Units = string.IsNullOrWhiteSpace(units) ? throw new ArgumentException("units is required", nameof(units)) : units;
    Directory = outputDir ?? Path.Combine(Path.GetTempPath(), "Speckle", "bundles", Guid.NewGuid().ToString("N"));
    BaseName = baseName ?? DEFAULT_BASE_NAME;
    System.IO.Directory.CreateDirectory(Directory);
    Pipeline = new ObjectsArtifactPipeline(Directory, BaseName, producer);
  }

  /// <summary>Who is writing the bundle.</summary>
  public ISpeckleApplication Producer { get; }

  public string Units { get; }
  public string Directory { get; }
  public string BaseName { get; }

  /// <summary>Objects added so far, in insertion order.</summary>
  public IReadOnlyCollection<BundleObject> Objects => _objects.Values;

  internal ObjectsArtifactPipeline Pipeline { get; }

  // ── containers (scene tree) ────────────────────────────────────────────────────────────────────────────────────────

  /// <summary>Gets or creates a CONTAINER chain for a name path (<c>["Level 1", "Walls"]</c>) — one node per segment,
  /// parent-linked, each keyed by its full path so two branches sharing a name stay distinct. Containers are the
  /// scene-tree nodes (spec node kind CONTAINER; <paramref name="subtype"/> says which flavour); objects join them
  /// through the <c>IN_COLLECTION</c> / <c>IN_MODEL</c> / <c>IN_GROUP</c> / <c>IN_SYSTEM</c> rels.</summary>
  /// <param name="subtype">Tag for the leaf (and any newly created ancestors): <c>"Layer"</c>, <c>"Category"</c>,
  /// <c>"Collection"</c> …</param>
  /// <param name="ghTopology">Grasshopper data-tree topology for the leaf (<c>nodes.gh_topology</c>); null otherwise.</param>
  public BundleContainer GetOrAddContainerPath(
    IReadOnlyList<string> path,
    string subtype = "Collection",
    string? ghTopology = null
  )
  {
    if (path.Count == 0)
    {
      throw new ArgumentException("A collection path needs at least one segment.", nameof(path));
    }
    BundleContainer? parent = null;
    string key = "";
    for (int i = 0; i < path.Count; i++)
    {
      string segment = path[i];
      key = key.Length == 0 ? segment : key + "/" + segment;
      bool leaf = i == path.Count - 1; // by position: a repeated segment name ("Mesh"/"Mesh") is not two leaves
      parent = GetOrAddContainer(key, segment, parent, subtype, leaf ? ghTopology : null);
    }
    return parent!;
  }

  /// <summary>Gets or creates one CONTAINER by explicit key — for containers that aren't a name path: a federated
  /// <c>Model</c>, a <c>Group</c>, an MEP <c>System</c>/<c>Network</c>.</summary>
  public BundleContainer GetOrAddContainer(
    string key,
    string? name,
    BundleContainer? parent,
    string subtype,
    string? ghTopology = null
  )
  {
    if (_containers.TryGetValue(key, out var existing))
    {
      Same(key, existing.Name, name, "name");
      Same(key, existing.Subtype, subtype, "subtype");
      Same(key, existing.Parent?.Key, parent?.Key, "parent");
      return existing;
    }
    int k = Pipeline.AddCollection(key, name, parent?.K, subtype, ghTopology);
    var c = new BundleContainer(this, k, key, name, subtype, parent);
    _containers[key] = c;
    return c;
  }

  // ── objects ───────────────────────────────────────────────────────────────────────────────────────────

  /// <summary>
  /// Gets or adds an object (the property carrier a host element becomes) by <paramref name="applicationId"/> — a
  /// handle only, nothing written yet. Describe it with <see cref="BundleObject.SetProperties"/>; point edges at it
  /// before or after (a joint a frame connects to may be described later).
  /// </summary>
  public BundleObject GetOrAddObject(string applicationId)
  {
    if (!_objects.TryGetValue(applicationId, out var obj))
    {
      obj = new BundleObject(this, Pipeline.InternObject(applicationId), applicationId);
      _objects[applicationId] = obj;
    }
    return obj;
  }

  internal void WriteProperties(
    BundleObject obj,
    IReadOnlyDictionary<string, object?>? properties,
    string? name,
    string? speckleType,
    string? sourceType,
    string? units,
    string? typeKey,
    IEnumerable<KeyValuePair<string, object?>>? rootScalars
  )
  {
    var scalars = new List<KeyValuePair<string, object?>>
    {
      new("speckle_type", speckleType),
      new("name", name),
      new("units", units ?? Units),
      new("type", sourceType),
    };
    if (rootScalars is not null)
    {
      scalars.AddRange(rootScalars);
    }
    Pipeline.AddProperties(obj.ApplicationId, properties ?? s_noProperties, scalars, typeKey);
  }

  /// <summary>Object handle by application id, if it has been added.</summary>
  public bool TryGetObject(string applicationId, [NotNullWhen(true)] out BundleObject? obj) =>
    _objects.TryGetValue(applicationId, out obj);

  /// <summary>Geometry handle by the key it was added under (explicit <c>geometryKey</c>, or the generated
  /// <c>{applicationId}:g{n}</c>), for post-loop edges that reference meshes by key — e.g. a definition that shares a
  /// mesh already written for an object.</summary>
  public bool TryGetGeometry(string geometryKey, [NotNullWhen(true)] out BundleGeometry? geometry) =>
    _geometries.TryGetValue(geometryKey, out geometry);

  internal BundleGeometry RegisterGeometry(string key, BundleGeometry geometry)
  {
    _geometries[key] = geometry;
    return geometry;
  }

  private static readonly IReadOnlyDictionary<string, object?> s_noProperties = new Dictionary<string, object?>();

  private static void Same<T>(string key, T written, T requested, string what)
  {
    if (!EqualityComparer<T>.Default.Equals(written, requested))
    {
      throw new InvalidOperationException(
        $"Key '{key}' was already added with {what} '{written}'; a second GetOrAdd asked for '{requested}'. "
          + "One key means one node — use a different key for different content."
      );
    }
  }

  // ── value nodes ───────────────────────────────────────────────────────────────────────────────────────

  /// <summary>Gets or creates a MATERIAL node. <paramref name="key"/> is the dedup identity (host material id);
  /// <paramref name="name"/> is the authored name receivers recreate the host material under.</summary>
  public BundleMaterial GetOrAddMaterial(
    string key,
    string? name,
    int argb,
    double opacity = 1,
    double metalness = 0,
    double roughness = 1,
    int? emissive = null,
    double? ior = null
  )
  {
    if (_materials.TryGetValue(key, out var existing))
    {
      Same(key, existing.Name, name, "name");
      Same(key, existing.Argb, argb, "argb");
      return existing;
    }
    int k = Pipeline.AddMaterial(key, name, argb, opacity, metalness, roughness, emissive, ior);
    var m = new BundleMaterial(this, k, key, name, argb);
    _materials[key] = m;
    return m;
  }

  /// <summary>Gets or creates a COLOR node, keyed by its ARGB value.</summary>
  public BundleColor GetOrAddColor(int argb)
  {
    if (_colors.TryGetValue(argb, out var existing))
    {
      return existing;
    }
    var c = new BundleColor(this, Pipeline.AddColor(argb), argb);
    _colors[argb] = c;
    return c;
  }

  /// <summary>Gets or creates a LEVEL node.</summary>
  public BundleLevel GetOrAddLevel(string key, string? name, double elevation)
  {
    if (_levels.TryGetValue(key, out var existing))
    {
      Same(key, existing.Name, name, "name");
      Same(key, existing.Elevation, elevation, "elevation");
      return existing;
    }
    var l = new BundleLevel(this, Pipeline.AddLevel(key, name, elevation), key, name, elevation);
    _levels[key] = l;
    return l;
  }

  /// <summary>Gets or creates a DEFINITION (block / family symbol). <paramref name="populate"/> runs only on first
  /// creation — put the definition's geometry, nested placements and member objects there.</summary>
  public BundleDefinition GetOrAddDefinition(string key, string? name, Action<BundleDefinition>? populate = null)
  {
    if (_definitions.TryGetValue(key, out var existing))
    {
      if (name is not null)
      {
        Same(key, existing.Name, name, "name"); // null = "whatever it was named" (a placement only knows the id)
      }
      return existing;
    }
    var d = new BundleDefinition(this, Pipeline.AddDefinition(key, name), key, name);
    _definitions[key] = d;
    populate?.Invoke(d);
    return d;
  }

  // ── model-scoped extras ───────────────────────────────────────────────────────────────────────────────

  /// <summary>A model-scoped attribute (<c>eav.model</c>): project information, the placement transform, document
  /// settings — anything with no owning object. Null values write nothing.</summary>
  public void ModelProperty(string path, object? value, string? unit = null) =>
    Pipeline.AddModelProperty(path, value, unit);

  /// <summary>One analysis-result row (<c>eav.structural_results</c>); see <see cref="ObjectsArtifactPipeline.AddStructuralResult"/>.</summary>
  public void StructuralResult(
    BundleObject? owner,
    string? location,
    string resultType,
    string loadCase,
    string component,
    double? station = null,
    int? step = null,
    double? value = null,
    string? valueText = null,
    string? elementName = null,
    string? positionLabel = null
  ) =>
    Pipeline.AddStructuralResult(
      owner?.ApplicationId,
      location,
      resultType,
      loadCase,
      component,
      station,
      step,
      value,
      valueText,
      elementName,
      positionLabel
    );

  /// <summary>One property-set field definition (<c>eav.property_set_definitions</c>), in authored field order.</summary>
  public void PropertySetDefinition(
    string setName,
    string setKey,
    string fieldName,
    string? fieldBucketId,
    string? dataType,
    string? defaultString = null,
    double? defaultDouble = null,
    bool? defaultBoolean = null,
    string? unit = null,
    string? description = null,
    string? setDescription = null,
    string? appliesTo = null
  ) =>
    Pipeline.AddPropertySetDefinition(
      setName,
      setKey,
      fieldName,
      fieldBucketId,
      dataType,
      defaultString,
      defaultDouble,
      defaultBoolean,
      unit,
      description,
      setDescription,
      appliesTo
    );

  /// <summary>A named camera viewpoint (<c>envelope.camera_views</c>).</summary>
  public void CameraView(CameraView view) => Pipeline.AddCameraView(view);

  /// <summary>
  /// Declares a scene view — how the viewer groups objects. Tiers are outermost first; each is either a relation
  /// (<see cref="SceneViewKey.Rel"/>: <c>IN_MODEL</c>, <c>ON_LEVEL</c>, <c>IN_COLLECTION</c> …) or a property value
  /// (<see cref="SceneViewKey.Eav"/>: <c>"category"</c>). When no view is declared by <see cref="Build"/>, the
  /// default view is the authored collection tree (<c>IN_COLLECTION</c>).
  /// </summary>
  public void SceneView(string name, bool isDefault, params SceneViewKey[] tiers)
  {
    var view = new SceneView(_sceneViews.Count, name, isDefault, tiers);
    _sceneViews.Add(view);
    Pipeline.AddSceneView(view);
  }

  // ── finish ────────────────────────────────────────────────────────────────────────────────────────────

  /// <summary>Flushes every table and returns the files. Adds the default <c>IN_COLLECTION</c> scene view if none
  /// was declared. Callable once.</summary>
  public BundleFiles Build()
  {
    if (_built)
    {
      throw new InvalidOperationException("Build() has already been called on this BundleBuilder.");
    }
    _built = true;
    if (_sceneViews.Count == 0)
    {
      SceneView("Default", isDefault: true, SceneViewKey.Rel(RelKind.InCollection));
    }
    Pipeline.SetProducer(Producer);
    Pipeline.Complete();

    var files = System
      .IO.Directory.EnumerateFiles(Directory, BaseName + ".*")
      .Where(p => p.EndsWith(".parquet", StringComparison.Ordinal))
      .OrderBy(p => p, StringComparer.Ordinal)
      .ToList();
    return new BundleFiles(Directory, BaseName, files, _objects.Count);
  }

  public void Dispose()
  {
    if (_disposed)
    {
      return;
    }
    _disposed = true;
    Pipeline.Dispose();
  }
}

/// <summary>The written bundle: where it is and what it contains. Hand <see cref="Files"/> to the uploader.</summary>
public sealed class BundleFiles
{
  internal BundleFiles(string directory, string baseName, IReadOnlyList<string> files, int objectCount)
  {
    Directory = directory;
    BaseName = baseName;
    Files = files;
    ObjectCount = objectCount;
  }

  public string Directory { get; }
  public string BaseName { get; }

  /// <summary>Full paths, sorted.</summary>
  public IReadOnlyList<string> Files { get; }

  public int ObjectCount { get; }

  /// <summary>File name → full path, as the v2 upload endpoints key files.</summary>
  public IReadOnlyDictionary<string, string> ByName =>
    Files.ToDictionary(p => Path.GetFileName(p), p => p, StringComparer.Ordinal);

  /// <summary>
  /// Re-keys every file from <see cref="BaseName"/> to <paramref name="versionId"/> (<c>{versionId}.eav.parquet</c> …) —
  /// the server pre-allocates the version id at ingestion time and keys uploads per basename, so a bundle built
  /// before the id is known is renamed rather than rewritten.
  /// </summary>
  public BundleFiles RenameTo(string versionId)
  {
    if (versionId == BaseName)
    {
      return this;
    }
    var renamed = new List<string>(Files.Count);
    foreach (var path in Files)
    {
      string name = Path.GetFileName(path);
#if NETSTANDARD2_0
      string target = Path.Combine(Directory, versionId + name.Substring(BaseName.Length));
#else
      string target = Path.Combine(Directory, string.Concat(versionId, name.AsSpan(BaseName.Length)));
#endif
      File.Move(path, target);
      renamed.Add(target);
    }
    return new BundleFiles(Directory, versionId, renamed, ObjectCount);
  }
}
