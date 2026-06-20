#if NET8_0_OR_GREATER
using System.Globalization;
using Speckle.Sdk.Models;
using Speckle.Sdk.Pipelines.Send.Artifacts;

namespace Speckle.Objects.Utils;

/// <summary>
/// Speckle 4.0 producer for the artefact TRIPLE (see <c>notes/topology-envelope-SOT.md</c>):
/// <c>geometries.parquet</c> (SGEO shape blobs, <see cref="GeometriesParquetWriter"/>),
/// <c>envelope.duckdb</c> (the topology property graph — relations + value-nodes,
/// <see cref="EnvelopeWriter"/>), and <c>eav.duckdb</c> (object set + identity dictionary +
/// per-object labels, <see cref="EavWriter"/>).
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
  private readonly GeometriesParquetWriter _geometriesWriter;
  private readonly EnvelopeWriter _envelopeWriter;
  private readonly EavWriter _eavWriter;
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
    _geometriesWriter = new GeometriesParquetWriter(outputDir, baseName);
    _envelopeWriter = new EnvelopeWriter(outputDir, baseName);
    _eavWriter = new EavWriter(outputDir, baseName);
    _excludedProperties = excludedTopLevelProperties ?? EavExtraction.DefaultExcludedTopLevelProperties;
  }

  /// <summary>The local path of the produced <c>geometries.parquet</c> file.</summary>
  public string GeometriesPath => _geometriesWriter.GeometriesPath;

  /// <summary>The local path of the produced <c>envelope.duckdb</c> (relations+nodes) file.</summary>
  public string EnvelopeDbPath => _envelopeWriter.EnvelopeDbPath;

  /// <summary>The local path of the produced <c>eav.duckdb</c> file.</summary>
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

    instanceProps = new Dictionary<string, object?>(properties, StringComparer.Ordinal) { ["Parameters"] = instanceParams };
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
      _envelopeWriter.AddNode(k, NodeKind.Definition, name, null, null, null, null, null, null, null, null);
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
      _envelopeWriter.AddNode(k, NodeKind.Material, null, null, null, null, argb, opacity, metalness, roughness, null);
    }
    return k;
  }

  /// <summary>Interns a COLOR value-node (keyed by its argb), writing it once.</summary>
  public int AddColor(int argb)
  {
    if (_nodeInterner.GetOrAdd("col:" + argb.ToString(CultureInfo.InvariantCulture), out var k))
    {
      _envelopeWriter.AddNode(k, NodeKind.Color, null, null, null, null, argb, null, null, null, null);
    }
    return k;
  }

  /// <summary>Interns a LEVEL value-node (name + elevation), writing it once.</summary>
  public int AddLevel(string levelKey, string? name, double elevation)
  {
    if (_nodeInterner.GetOrAdd("lvl:" + levelKey, out var k))
    {
      _envelopeWriter.AddNode(k, NodeKind.Level, name, null, null, null, null, null, null, null, elevation);
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

  /// <summary>node(DEFINITION) → geometry | node(nested INSTANCE): definition membership.</summary>
  public void Defines(int definitionK, int memberK, int ord) =>
    _envelopeWriter.AddRelation(RelKind.Defines, definitionK, memberK, ord);

  /// <summary>geometry → node(MATERIAL): per-mesh render material.</summary>
  public void HasMaterial(int geometryK, int materialK) =>
    _envelopeWriter.AddRelation(RelKind.HasMaterial, geometryK, materialK, 0);

  /// <summary>geometry | object → node(COLOR): display colour.</summary>
  public void HasColor(int srcK, int colorK) => _envelopeWriter.AddRelation(RelKind.HasColor, srcK, colorK, 0);

  /// <summary>object → node(LEVEL): level membership.</summary>
  public void OnLevel(int objectK, int levelK) => _envelopeWriter.AddRelation(RelKind.OnLevel, objectK, levelK, 0);

  /// <summary>REMOVED — the <c>proxies(type, data JSON)</c> envelope is gone; use the typed
  /// node/relation API (<see cref="AddDefinition"/>, <see cref="AddMaterial"/>, <see cref="Display"/>, …).
  /// Kept (non-<c>[Obsolete]</c>, to avoid breaking the warnings-as-errors build of the parked Navis
  /// path) as a throwing stub; it fails loudly only if actually invoked.</summary>
  public void AddProxy(string type, string dataJson) =>
    throw new NotSupportedException(
      "AddProxy was removed with the proxies(type,json) envelope. Use the typed relations+nodes API."
    );

  /// <summary>Flushes and closes all three files (releases the DuckDB locks).</summary>
  public void Complete()
  {
    _geometriesWriter.Complete();
    _envelopeWriter.Complete();
    _eavWriter.Complete();
  }

  // Cleanup path: dispose every writer independently and never let one writer's cleanup
  // error escape (it fires during exception unwind).
  public void Dispose()
  {
    SafeDispose(_geometriesWriter);
    SafeDispose(_envelopeWriter);
    SafeDispose(_eavWriter);
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
