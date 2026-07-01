#if NETSTANDARD2_0 || NET8_0_OR_GREATER
using System.Globalization;
using Speckle.Sdk.Models;
using Speckle.Sdk.Pipelines.Send.Artifacts;

namespace Speckle.Objects.Utils;

/// <summary>
/// Speckle 4.0 producer for the artefact bundle (see <c>notes/topology-envelope-SOT.md</c>),
/// now PARQUET-ONLY — direct Zstd parquet, one file per table, no DuckDB:
/// <c>geometries.parquet</c> (SGEO shape blobs, <see cref="GeometriesParquetWriter"/>),
/// the <c>envelope.*.parquet</c> table set (the topology property graph — relations + value-nodes,
/// <see cref="EnvelopeWriter"/>), and the <c>eav.*.parquet</c> table set (object set + identity
/// dictionary + per-object labels, <see cref="EavWriter"/>).
///
/// This class owns the three per-namespace identity interners and exposes a typed emit API
/// so the producer stays string-based while the artefacts store pure dense <c>int32</c>:
/// <list type="bullet">
///   <item><b>object</b> namespace — interned by <see cref="EavWriter.GetOrAddObject"/>
///   (eav is the dictionary home); resolved here via <see cref="InternObject"/>.</item>
///   <item><b>geometry</b> namespace — <see cref="_geometryInterner"/>; one row per mesh.</item>
///   <item><b>node</b> namespace — <see cref="_nodeInterner"/> (kind-prefixed keys);
///   definitions / instances / materials / colours / levels.</item>
/// </list>
/// Producing the files is decoupled from uploading: write here, then hand
/// <see cref="GeometriesPath"/> + <see cref="EnvelopeDbPath"/> + <see cref="EavDbPath"/> to
/// the uploader (stubbed for the POC).
/// </summary>
public sealed class ObjectsArtifactPipeline : IDisposable
{
  // One background writer thread shared by all three artefacts. The ODA extraction thread interns +
  // buffers rows synchronously; every row-group flush + file finalize is handed to this scheduler, so
  // Parquet's sync-over-async IO never runs on the ODA pinned thread (no deadlock), overlaps extraction,
  // and is bounded by the scheduler's queue (backpressure). See ParquetWriteScheduler.
  private readonly ParquetWriteScheduler _scheduler = new();
  private readonly GeometriesParquetWriter _geometriesWriter;
  private readonly EnvelopeWriter _envelopeWriter;
  private readonly EavWriter _eavWriter;
  private readonly StructuralResultsWriter _structuralResultsWriter;
  private readonly ISet<string> _excludedProperties;

  // Per-namespace interners. The object namespace is owned by the eav writer (it writes the
  // dictionary), so it is not duplicated here.
  private readonly IdInterner _geometryInterner = new();
  private readonly IdInterner _nodeInterner = new();

  public ObjectsArtifactPipeline(
    string outputDir,
    string baseName,
    ISet<string>? excludedTopLevelProperties = null
  )
  {
    _geometriesWriter = new GeometriesParquetWriter(outputDir, baseName, _scheduler);
    _envelopeWriter = new EnvelopeWriter(outputDir, baseName, _scheduler);
    _eavWriter = new EavWriter(outputDir, baseName, _scheduler);
    _structuralResultsWriter = new StructuralResultsWriter(outputDir, baseName, _scheduler);
    _excludedProperties = excludedTopLevelProperties ?? EavExtraction.DefaultExcludedTopLevelProperties;
  }

  /// <summary>The local path of the produced <c>geometries.parquet</c> file.</summary>
  public string GeometriesPath => _geometriesWriter.GeometriesPath;

  /// <summary>The output directory holding the produced <c>envelope.*.parquet</c> tables
  /// (relations + nodes). Name kept (<c>...DbPath</c>) for caller compatibility — no DuckDB is written.</summary>
  public string EnvelopeDbPath => _envelopeWriter.EnvelopeDbPath;

  /// <summary>The output directory holding the produced <c>eav.*.parquet</c> tables.
  /// Name kept (<c>...DbPath</c>) for caller compatibility — no DuckDB is written.</summary>
  public string EavDbPath => _eavWriter.EavDbPath;

  // ── object namespace ──────────────────────────────────────────────────────────────

  /// <summary>Resolves an object's dense <c>K</c> (interns its <paramref name="applicationId"/>
  /// via the eav dictionary), so the caller can emit its envelope edges with the SAME id eav uses.</summary>
  public int InternObject(string applicationId) => _eavWriter.GetOrAddObject(applicationId);

  /// <summary>
  /// Flattens an object's property tree into <c>eav</c> keyed by
  /// <paramref name="applicationId"/>. <paramref name="properties"/> is the merged-ancestry
  /// dictionary (geometry excluded by construction); <paramref name="rootScalars"/> are bare
  /// top-level labels (speckle_type, name, units, …).
  /// </summary>
  public void AddProperties(
    string applicationId,
    IReadOnlyDictionary<string, object?> properties,
    IEnumerable<KeyValuePair<string, object?>>? rootScalars = null,
    string? typeKey = null
  )
  {
    // No type key (or nothing type-scoped to split) → flatten everything per-object, as before.
    if (typeKey is null || !TrySplitTypeParameters(properties, out var instanceProps, out var typeSubtree))
    {
      var rows = new List<EavRow>();
      EavExtraction.FlattenProperties(applicationId, properties, rootScalars, _excludedProperties, rows);
      _eavWriter.AddRows(applicationId, rows);
      return;
    }

    // Instance-scoped props → eav; Type/System params deduped into type_eav (flattened once per type,
    // via the lazy factory) with an object_type weak ref. See notes/topology-envelope-SOT.md §6.
    var instanceRows = new List<EavRow>();
    EavExtraction.FlattenProperties(applicationId, instanceProps, rootScalars, _excludedProperties, instanceRows);
    _eavWriter.AddRows(applicationId, instanceRows);

    _eavWriter.AddType(
      applicationId,
      typeKey,
      () =>
      {
        var typeRows = new List<EavRow>();
        EavExtraction.FlattenSubtree(typeSubtree, "properties.Parameters", typeRows);
        return typeRows;
      }
    );
  }

  // Splits `properties.Parameters` into instance-scoped (kept on the object) and type-scoped (Type +
  // System Parameters, deduped per type). False if there's nothing type-scoped to split out.
  private static bool TrySplitTypeParameters(
    IReadOnlyDictionary<string, object?> properties,
    out IReadOnlyDictionary<string, object?> instanceProps,
    out IReadOnlyDictionary<string, object?> typeSubtree
  )
  {
    instanceProps = properties;
    typeSubtree = s_emptyDict;

    if (
      !properties.TryGetValue("Parameters", out var pv)
      || pv is not IReadOnlyDictionary<string, object?> paramsDict
    )
    {
      return false;
    }

    var typeParams = new Dictionary<string, object?>(StringComparer.Ordinal);
    var instanceParams = new Dictionary<string, object?>(StringComparer.Ordinal);
    foreach (var kv in paramsDict)
    {
      if (kv.Key is "Type Parameters" or "System Type Parameters")
      {
        typeParams[kv.Key] = kv.Value;
      }
      else
      {
        instanceParams[kv.Key] = kv.Value;
      }
    }

    if (typeParams.Count == 0)
    {
      return false;
    }

    // Copy via foreach (the Dictionary(IEnumerable<KVP>, comparer) ctor is net5+; netstandard2.0 only has the
    // IDictionary ctor, and `properties` is an IReadOnlyDictionary).
    var merged = new Dictionary<string, object?>(StringComparer.Ordinal);
    foreach (var kv in properties)
    {
      merged[kv.Key] = kv.Value;
    }
    merged["Parameters"] = instanceParams;
    instanceProps = merged;
    typeSubtree = typeParams;
    return true;
  }

  private static readonly IReadOnlyDictionary<string, object?> s_emptyDict = new Dictionary<string, object?>();

  // ── geometry namespace ────────────────────────────────────────────────────────────

  /// <summary>
  /// Interns <paramref name="meshApplicationId"/> to a dense geometry <c>K</c>, encoding +
  /// storing the SGEO blob on first sight, and returns the <c>K</c> (for <c>DISPLAY</c>/
  /// <c>DEFINES</c>/<c>HAS_MATERIAL</c> edges). Re-encoding is skipped on repeats.
  /// </summary>
  public int AddGeometry(string meshApplicationId, Base geometry)
  {
    if (_geometryInterner.GetOrAdd(meshApplicationId, out var geometryK))
    {
      _geometriesWriter.AddGeometry(geometryK, SgeoEncoder.Encode(geometry));
    }
    return geometryK;
  }

  /// <summary>
  /// Interns <paramref name="geometryApplicationId"/> to a dense geometry <c>K</c> and stores the RAW
  /// <paramref name="content"/> bytes verbatim (no SGEO encoding) with an explicit <paramref name="type"/>
  /// label on first sight, returning the <c>K</c>. Use for host-native geometry kept losslessly for
  /// receive — e.g. a Rhino Brep/Extrusion/SubD serialized to a 3dm blob (<c>type = "3dm"</c>) linked via
  /// the <c>SOLID</c> rel, alongside its <c>DISPLAY</c> meshes added through <see cref="AddGeometry"/>.
  /// </summary>
  public int AddRawGeometry(string geometryApplicationId, byte[] content, string type)
  {
    if (_geometryInterner.GetOrAdd(geometryApplicationId, out var geometryK))
    {
      _geometriesWriter.AddRawGeometry(geometryK, content, type);
    }
    return geometryK;
  }

  /// <summary>Resolves the geometry <c>K</c> for an already-added mesh (lookup, no encode) —
  /// for the post-loop <c>DEFINES</c>/<c>HAS_MATERIAL</c> edges that reference meshes by their
  /// host applicationId.</summary>
  public int InternGeometryId(string meshApplicationId) => _geometryInterner.GetOrAdd(meshApplicationId);

  // ── node namespace (value-entities) ────────────────────────────────────────────────

  /// <summary>Interns a DEFINITION node (instance-definition / block), writing it once.</summary>
  public int AddDefinition(string definitionKey, string? name)
  {
    if (_nodeInterner.GetOrAdd("def:" + definitionKey, out var k))
    {
      _envelopeWriter.AddNode(k, NodeKind.Definition, name, null, null, null, null, null, null, null, null, null);
    }
    return k;
  }

  /// <summary>Interns an INSTANCE (placement) node — its transform + the DEFINITION it
  /// references. <paramref name="transform"/> is 16 row-major doubles.</summary>
  public int AddInstance(string placementKey, int defRef, IReadOnlyList<double> transform, string? units)
  {
    if (_nodeInterner.GetOrAdd("inst:" + placementKey, out var k))
    {
      _envelopeWriter.AddNode(
        k,
        NodeKind.Instance,
        null,
        defRef,
        FormatTransform(transform),
        units,
        null,
        null,
        null,
        null,
        null,
        null
      );
    }
    return k;
  }

  /// <summary>Interns a MATERIAL value-node (inline render value), writing it once.</summary>
  public int AddMaterial(string materialKey, int argb, double opacity, double metalness, double roughness)
  {
    if (_nodeInterner.GetOrAdd("mat:" + materialKey, out var k))
    {
      _envelopeWriter.AddNode(k, NodeKind.Material, null, null, null, null, null, argb, opacity, metalness, roughness, null);
    }
    return k;
  }

  /// <summary>Interns a COLOR value-node (keyed by its argb), writing it once.</summary>
  public int AddColor(int argb)
  {
    if (_nodeInterner.GetOrAdd("col:" + argb.ToString(CultureInfo.InvariantCulture), out var k))
    {
      _envelopeWriter.AddNode(k, NodeKind.Color, null, null, null, null, null, argb, null, null, null, null);
    }
    return k;
  }

  /// <summary>Interns a LEVEL value-node (name + elevation), writing it once.</summary>
  public int AddLevel(string levelKey, string? name, double elevation)
  {
    if (_nodeInterner.GetOrAdd("lvl:" + levelKey, out var k))
    {
      _envelopeWriter.AddNode(k, NodeKind.Level, name, null, null, null, null, null, null, null, null, elevation);
    }
    return k;
  }

  /// <summary>Interns a scene-tree collection (layer / category / story) node, writing it once. In v5 a
  /// collection is a CONTAINER node whose <c>subtype</c> carries its tag; the <c>IN_COLLECTION</c> rel marks
  /// the grouping axis (distinct from IN_MODEL / IN_SYSTEM / …). <paramref name="parentCollectionK"/> is its
  /// parent collection node (null = top-level, directly under the excluded root) — the parent chain IS the
  /// source hierarchy. <paramref name="subtype"/> is a tag (e.g. "Layer" / "Collection" / source
  /// collectionType) carried in the <c>subtype</c> column for the loader to label it.</summary>
  public int AddCollection(string collectionKey, string? name, int? parentCollectionK, string? subtype)
  {
    if (_nodeInterner.GetOrAdd("coll:" + collectionKey, out var k))
    {
      // v5: a collection is a CONTAINER whose `subtype` carries its tag; the IN_COLLECTION rel marks the axis.
      _envelopeWriter.AddNode(k, NodeKind.Container, name, parentCollectionK, null, null, subtype, null, null, null, null, null);
    }
    return k;
  }

  /// <summary>Interns a CONTAINER (semantic-topology bucket: model / room / system / …) node, writing it
  /// once. Distinct from <see cref="AddCollection"/> (the authored scene-tree). <paramref name="parentContainerK"/>
  /// is its parent CONTAINER (null = top-level) — self-nesting for nested links / appended files.
  /// <paramref name="subtype"/> is the canonical axis tag carried in the <c>subtype</c> column (e.g. "Model");
  /// use the SAME tag across connectors for the same concept (see the <see cref="RelKind"/> naming convention).</summary>
  public int AddContainer(string containerKey, string? name, int? parentContainerK, string? subtype)
  {
    if (_nodeInterner.GetOrAdd("cont:" + containerKey, out var k))
    {
      _envelopeWriter.AddNode(k, NodeKind.Container, name, parentContainerK, null, null, subtype, null, null, null, null, null);
    }
    return k;
  }

  // ── relations ──────────────────────────────────────────────────────────────────────

  /// <summary>object → geometry: direct renderable geometry (world-coord mesh).</summary>
  public void Display(int objectK, int geometryK, int ord) =>
    _envelopeWriter.AddRelation(RelKind.Display, objectK, geometryK, ord);

  /// <summary>object → node(INSTANCE): renderable via a placement (transform + definition).</summary>
  public void DisplayInstance(int objectK, int instanceK, int ord) =>
    _envelopeWriter.AddRelation(RelKind.DisplayInstance, objectK, instanceK, ord);

  /// <summary>object → geometry: authoritative solid.</summary>
  public void Solid(int objectK, int geometryK, int ord) =>
    _envelopeWriter.AddRelation(RelKind.Solid, objectK, geometryK, ord);

  /// <summary>object → object: host→hosted (curtain wall → panel).</summary>
  public void Subelement(int parentObjectK, int childObjectK, int ord) =>
    _envelopeWriter.AddRelation(RelKind.Subelement, parentObjectK, childObjectK, ord);

  /// <summary>node(DEFINITION) → geometry: definition contains a raw mesh member.</summary>
  public void Defines(int definitionK, int geometryK, int ord) =>
    _envelopeWriter.AddRelation(RelKind.Defines, definitionK, geometryK, ord);

  /// <summary>node(DEFINITION) → node(nested INSTANCE): definition contains a nested block placement.
  /// Distinct rel from <see cref="Defines"/> so <c>rel</c> fixes the dst namespace (node, not geometry).</summary>
  public void DefinesInstance(int definitionK, int instanceK, int ord) =>
    _envelopeWriter.AddRelation(RelKind.DefinesInstance, definitionK, instanceK, ord);

  /// <summary>geometry → node(MATERIAL): per-mesh render material.</summary>
  public void HasMaterial(int geometryK, int materialK) =>
    _envelopeWriter.AddRelation(RelKind.HasMaterial, geometryK, materialK, 0);

  /// <summary>geometry | object → node(COLOR): display colour.</summary>
  public void HasColor(int srcK, int colorK) => _envelopeWriter.AddRelation(RelKind.HasColor, srcK, colorK, 0);

  /// <summary>object → node(LEVEL): level membership.</summary>
  public void OnLevel(int objectK, int levelK) => _envelopeWriter.AddRelation(RelKind.OnLevel, objectK, levelK, 0);

  /// <summary>object → node(COLLECTION): the object's direct membership in a scene-tree container.</summary>
  public void InCollection(int objectK, int collectionK, int ord) =>
    _envelopeWriter.AddRelation(RelKind.InCollection, objectK, collectionK, ord);

  /// <summary>object → node(CONTAINER, subtype "Model"): the object's source-document / host / linked-model
  /// membership. The default-projection top key for Revit links / Navis federated files (SOT §8).</summary>
  public void InModel(int objectK, int modelK, int ord) =>
    _envelopeWriter.AddRelation(RelKind.InModel, objectK, modelK, ord);

  /// <summary>object → node(CONTAINER, subtype "Room"): room containment.</summary>
  public void InRoom(int objectK, int roomK, int ord) =>
    _envelopeWriter.AddRelation(RelKind.InRoom, objectK, roomK, ord);

  /// <summary>object → node(CONTAINER, subtype "System"): named logical engineering system membership
  /// (Revit MEPSystem, IFC IfcDistributionSystem). Also the v5 home of physically-connected NETWORKS —
  /// a network is a CONTAINER with subtype "Network" reached through this same rel (the IN_NETWORK rel was
  /// collapsed into IN_SYSTEM).</summary>
  public void InSystem(int objectK, int systemK, int ord) =>
    _envelopeWriter.AddRelation(RelKind.InSystem, objectK, systemK, ord);

  /// <summary>object → object: physical flow connectivity, DIRECTED src→dst by flow (source→target). A
  /// reciprocal pair encodes undirected / unknown flow.</summary>
  public void ConnectsTo(int sourceObjectK, int targetObjectK) =>
    _envelopeWriter.AddRelation(RelKind.ConnectsTo, sourceObjectK, targetObjectK, 0);

  // ── structural results ─────────────────────────────────────────────────────────────────

  /// <summary>
  /// Appends one structural analysis/design result value to <c>{base}.eav.structural-results.parquet</c>
  /// (see <see cref="StructuralResultsWriter"/>). <b>Object-level</b> results pass the member/joint's
  /// <paramref name="objectApplicationId"/> (resolved to the SAME dense K the object was interned with, so
  /// results join back to it) and leave <paramref name="location"/> null; <b>model-level</b> results (story
  /// drift, modal period, base reaction) pass a null <paramref name="objectApplicationId"/> and identify via
  /// <paramref name="location"/> (story) and/or <paramref name="step"/> (mode). Numeric results set
  /// <paramref name="value"/>; non-numeric design verdicts set <paramref name="valueText"/>.
  /// </summary>
  public void AddStructuralResult(
    string? objectApplicationId,
    string? location,
    string resultType,
    string loadCase,
    string component,
    double? station,
    int? step,
    double? value,
    string? valueText = null
  )
  {
    int? objectIndex = objectApplicationId is null ? null : _eavWriter.GetOrAddObject(objectApplicationId);
    _structuralResultsWriter.AddRow(objectIndex, location, resultType, loadCase, component, station, step, value, valueText);
  }

  // ── scene views ──────────────────────────────────────────────────────────────────────

  /// <summary>Authors a scene_views projection (SOT §8): the producer's default (and optional named
  /// alternate) scene-explorer grouping, which the consumer seeds the model-tree grouping from. Build keys
  /// with <see cref="SceneViewKey.Rel"/> / <see cref="SceneViewKey.Eav"/>. Omit keys with no data.</summary>
  public void AddSceneView(SceneView view) => _envelopeWriter.AddSceneView(view);

  /// <summary>REMOVED — the <c>proxies(type, data JSON)</c> envelope is gone; use the typed
  /// node/relation API (<see cref="AddDefinition"/>, <see cref="AddMaterial"/>, <see cref="Display"/>, …).
  /// Kept (non-<c>[Obsolete]</c>, to avoid breaking the warnings-as-errors build of the parked Navis
  /// path) as a throwing stub; it fails loudly only if actually invoked.</summary>
  public void AddProxy(string type, string dataJson) =>
    throw new NotSupportedException(
      "AddProxy was removed with the proxies(type,json) envelope. Use the typed relations+nodes API."
    );

  /// <summary>
  /// Enqueues every artefact's final flush + finalize, then BLOCKS until the background writer has
  /// drained — so all parquet files are fully written and closed on return. The uploader reads
  /// <see cref="GeometriesPath"/>/<see cref="EnvelopeDbPath"/>/<see cref="EavDbPath"/> only after this.
  /// Re-throws on this thread if any background write faulted.
  /// </summary>
  public void Complete()
  {
    _geometriesWriter.Complete();
    _envelopeWriter.Complete();
    _eavWriter.Complete();
    _structuralResultsWriter.Complete();
    _scheduler.CompleteAndWait();
  }

  // Cleanup path: enqueue each writer's finalize (best-effort), then drain + join the background
  // writer so file handles close. Never let one writer's cleanup error escape (it fires during unwind).
  public void Dispose()
  {
    SafeDispose(_geometriesWriter);
    SafeDispose(_envelopeWriter);
    SafeDispose(_eavWriter);
    SafeDispose(_structuralResultsWriter);
    SafeDispose(_scheduler);
  }

  private static string FormatTransform(IReadOnlyList<double> transform) =>
    string.Join(",", transform.Select(d => d.ToString("R", CultureInfo.InvariantCulture)));

  private static void SafeDispose(IDisposable writer)
  {
    try
    {
      writer.Dispose();
    }
#pragma warning disable CA1031 // cleanup path: swallow so the original failure propagates unmasked
    catch (Exception)
#pragma warning restore CA1031
    {
      // Intentionally ignored.
    }
  }
}
#endif
