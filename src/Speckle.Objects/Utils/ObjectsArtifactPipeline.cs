#if NET8_0_OR_GREATER
using Speckle.Newtonsoft.Json.Linq;
using Speckle.Sdk.Models;
using Speckle.Sdk.Pipelines.Send.Artifacts;

namespace Speckle.Objects.Utils;

/// <summary>
/// Speckle 4.0 producer for the full-binary artifact PAIR: <c>objects.duckdb</c>
/// (SGEO geometries keyed by applicationId + topology/material proxies, via
/// <see cref="ObjectsArtifactWriter"/>) and <c>eav.duckdb</c> (flattened
/// properties keyed by applicationId, via <see cref="ApplicationIdEavWriter"/> —
/// distinct from the envelope path's object-id-keyed properties table). Geometry
/// is encoded with <see cref="SgeoEncoder"/>; EAV uses the shared
/// <see cref="EavExtraction"/>, re-keyed to applicationId.
///
/// Producing the files is decoupled from uploading them: write here, then hand
/// <see cref="ObjectsDbPath"/> + <see cref="EavDbPath"/> to
/// <c>ArtifactPipeline.UploadFilesAsync</c>. See
/// <c>plans/speckle-4.0/objects-duckdb-proxies-sgeo.md</c>.
/// </summary>
public sealed class ObjectsArtifactPipeline : IDisposable
{
  private readonly ObjectsArtifactWriter _writer;
  private readonly ApplicationIdEavWriter _eavWriter;
  private readonly ISet<string> _excludedProperties;

  public ObjectsArtifactPipeline(
    string outputDir,
    string baseName,
    ISet<string>? excludedTopLevelProperties = null
  )
  {
    _writer = new ObjectsArtifactWriter(outputDir, baseName);
    _eavWriter = new ApplicationIdEavWriter(outputDir, baseName);
    // Shared canonical exclusion list (Autodesk Material, Document, …) so the
    // binary and envelope eav files drop the same categories.
    _excludedProperties = excludedTopLevelProperties ?? EavExtraction.DefaultExcludedTopLevelProperties;
  }

  /// <summary>The local path of the produced <c>objects.duckdb</c> file.</summary>
  public string ObjectsDbPath => _writer.ObjectsDbPath;

  /// <summary>The local path of the produced (applicationId-keyed) <c>eav.duckdb</c> file.</summary>
  public string EavDbPath => _eavWriter.EavDbPath;

  /// <summary>
  /// Encodes <paramref name="geometry"/> to SGEO and stores it under
  /// <paramref name="applicationId"/> in the <c>geometries</c> table. A given
  /// object may be added several times for a multi-geometry display value.
  /// </summary>
  public void AddGeometry(string applicationId, Base geometry) =>
    _writer.AddGeometry(applicationId, SgeoEncoder.Encode(geometry));

  /// <summary>
  /// Adds a topology/attribute proxy row (type ∈ instanceDef | layer | material
  /// | colour | group | level; <paramref name="dataJson"/> is its JSON envelope).
  /// </summary>
  public void AddProxy(string type, string dataJson) => _writer.AddProxy(type, dataJson);

  /// <summary>
  /// Flattens an object's properties into the <c>eav</c> table keyed by
  /// <paramref name="applicationId"/>. <paramref name="objectJson"/> is the
  /// object's JSON (root scalars + a <c>properties</c> subtree); geometry is
  /// expected to be excluded by the caller.
  /// </summary>
  public void AddProperties(string applicationId, JObject objectJson) =>
    _eavWriter.AddRows(
      applicationId,
      EavExtraction.FlattenObjectProperties(applicationId, objectJson, _excludedProperties)
    );

  /// <summary>Flushes and closes both files (releases the DuckDB locks).</summary>
  public void Complete()
  {
    _writer.Complete();
    _eavWriter.Complete();
  }

  public void Dispose()
  {
    _writer.Dispose();
    _eavWriter.Dispose();
  }
}
#endif
