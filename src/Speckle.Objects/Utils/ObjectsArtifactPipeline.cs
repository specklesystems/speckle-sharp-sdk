#if NET8_0_OR_GREATER
using Speckle.Sdk.Models;
using Speckle.Sdk.Pipelines.Send.Artifacts;

namespace Speckle.Objects.Utils;

/// <summary>
/// Speckle 4.0 producer for the full-binary artifact TRIPLE (Integrations Board
/// final shape): <c>geometries.duckdb</c> (SGEO geometry blobs keyed by
/// applicationId, via <see cref="GeometriesArtifactWriter"/>),
/// <c>envelope.duckdb</c> (lean topology/material proxies, via
/// <see cref="EnvelopeArtifactWriter"/>), and <c>eav.duckdb</c> (flattened
/// properties keyed by applicationId, via <see cref="ApplicationIdEavWriter"/> —
/// distinct from the envelope path's object-id-keyed properties table). Geometry
/// is encoded with <see cref="SgeoEncoder"/>; EAV uses the shared
/// <see cref="EavExtraction"/>, re-keyed to applicationId.
///
/// Producing the files is decoupled from uploading them: write here, then hand
/// <see cref="GeometriesPath"/> + <see cref="EnvelopeDbPath"/> +
/// <see cref="EavDbPath"/> to <c>ArtifactPipeline.UploadFilesAsync</c>. See
/// <c>plans/speckle-4.0/objects-duckdb-proxies-sgeo.md</c>.
/// </summary>
public sealed class ObjectsArtifactPipeline : IDisposable
{
  private readonly GeometriesParquetWriter _geometriesWriter;
  private readonly EnvelopeArtifactWriter _envelopeWriter;
  private readonly CompactEavWriter _eavWriter;
  private readonly ISet<string> _excludedProperties;

  public ObjectsArtifactPipeline(
    string outputDir,
    string baseName,
    ISet<string>? excludedTopLevelProperties = null
  )
  {
    _geometriesWriter = new GeometriesParquetWriter(outputDir, baseName);
    _envelopeWriter = new EnvelopeArtifactWriter(outputDir, baseName);
    _eavWriter = new CompactEavWriter(outputDir, baseName);
    // Shared canonical exclusion list (Autodesk Material, Document, …) so the
    // binary and envelope eav files drop the same categories.
    _excludedProperties = excludedTopLevelProperties ?? EavExtraction.DefaultExcludedTopLevelProperties;
  }

  /// <summary>The local path of the produced <c>geometries.parquet</c> file.</summary>
  public string GeometriesPath => _geometriesWriter.GeometriesPath;

  /// <summary>The local path of the produced <c>envelope.duckdb</c> (proxies) file.</summary>
  public string EnvelopeDbPath => _envelopeWriter.EnvelopeDbPath;

  /// <summary>The local path of the produced (applicationId-keyed) <c>eav.duckdb</c> file.</summary>
  public string EavDbPath => _eavWriter.EavDbPath;

  /// <summary>
  /// Encodes <paramref name="geometry"/> to SGEO and stores it under
  /// <paramref name="applicationId"/> in the <c>geometries</c> table. A given
  /// object may be added several times for a multi-geometry display value.
  /// </summary>
  public void AddGeometry(string applicationId, Base geometry) =>
    _geometriesWriter.AddGeometry(applicationId, SgeoEncoder.Encode(geometry));

  /// <summary>
  /// Adds a topology/attribute proxy row (type ∈ instanceDef | layer | material
  /// | colour | group | level; <paramref name="dataJson"/> is its JSON envelope)
  /// to <c>envelope.duckdb</c>.
  /// </summary>
  public void AddProxy(string type, string dataJson) => _envelopeWriter.AddProxy(type, dataJson);

  /// <summary>
  /// Flattens an object's native property tree straight into the <c>eav</c> table
  /// keyed by <paramref name="applicationId"/> — no <c>JObject.FromObject</c> /
  /// <c>EavExtraction.FlattenObjectProperties(JObject)</c> round-trip.
  /// <paramref name="properties"/> is the merged-ancestry Dictionary from the
  /// extractor (geometry excluded by construction); <paramref name="rootScalars"/>
  /// are bare top-level fields (speckle_type, name, units, …).
  /// </summary>
  public void AddProperties(
    string applicationId,
    IReadOnlyDictionary<string, object?> properties,
    IEnumerable<KeyValuePair<string, object?>>? rootScalars = null
  )
  {
    var rows = new List<EavRow>();
    EavExtraction.FlattenProperties(applicationId, properties, rootScalars, _excludedProperties, rows);
    _eavWriter.AddRows(applicationId, rows);
  }

  /// <summary>Flushes and closes all three files (releases the DuckDB locks).</summary>
  public void Complete()
  {
    _geometriesWriter.Complete();
    _envelopeWriter.Complete();
    _eavWriter.Complete();
  }

  // Cleanup path: dispose every writer independently and never let one writer's
  // cleanup error escape. On a failing run this fires during exception unwind, so a
  // throwing Dispose would (a) skip the remaining writers and (b) mask the real
  // exception. Success closes the writers via Complete(); this is just the net.
  public void Dispose()
  {
    SafeDispose(_geometriesWriter);
    SafeDispose(_envelopeWriter);
    SafeDispose(_eavWriter);
  }

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
