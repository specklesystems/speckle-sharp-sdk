#if NET8_0_OR_GREATER
using DuckDB.NET.Data;
using Speckle.Newtonsoft.Json;
using Speckle.Newtonsoft.Json.Linq;

namespace Speckle.Sdk.Pipelines.Send.Artifacts;

/// <summary>
/// POC: writes the purpose-specific DuckDB artifacts client-side, during send,
/// eliminating the server's NDJSON re-parse / packfile materialization for
/// these files (Speckle 4.0 direction — see Speckle-4.0-Plan.md).
///
/// Produces two files in <c>outputDir</c>:
/// <list type="bullet">
/// <item><c>{baseName}_viewer.duckdb</c> — same table shape as today's server
/// packfile (objects / root / blobs), with two reductions:
/// DataObject envelopes have <c>properties</c> stripped (they live in eav),
/// and Blob bytes are read from the envelope's local <c>filePath</c> into the
/// <c>blobs</c> table (uncompressed for now — POC keeps C# simple). Geometry
/// bytes do NOT travel on the v1 NDJSON wire — this file is their only home.</item>
/// <item><c>{baseName}_eav.duckdb</c> — the <c>properties</c> table, produced
/// by <see cref="EavExtraction"/> (client-side port of the server's
/// flattenObjectProperties).</item>
/// </list>
///
/// Not thread-safe: <see cref="Add"/> is expected to be called sequentially
/// (matches SendPipeline.Process usage).
/// </summary>
public sealed class DuckDbArtifactWriter : IDisposable
{
  private const string BLOB_SPECKLE_TYPE = "Speckle.Core.Models.Blob";

  public string ViewerDbPath { get; }
  public string EavDbPath { get; }

  private readonly DuckDBConnection _viewerDb;
  private readonly DuckDBConnection _eavDb;
  private readonly DuckDBAppender _objectsAppender;
  private readonly DuckDBAppender _blobsAppender;
  private readonly DuckDBAppender _propsAppender;
  private readonly HashSet<string> _seen = new();
  private (string Id, string Json)? _lastItem;
  private bool _completed;

  public DuckDbArtifactWriter(string outputDir, string baseName)
  {
    Directory.CreateDirectory(outputDir);
    ViewerDbPath = Path.Combine(outputDir, $"{baseName}_viewer.duckdb");
    EavDbPath = Path.Combine(outputDir, $"{baseName}_eav.duckdb");

    // Stale files from a previous run make CREATE TABLE fail.
    DeleteIfExists(ViewerDbPath);
    DeleteIfExists(EavDbPath);

    _viewerDb = new DuckDBConnection($"Data Source={ViewerDbPath}");
    _viewerDb.Open();
    Execute(
      _viewerDb,
      "CREATE TABLE objects (id VARCHAR PRIMARY KEY, data JSON, speckle_type VARCHAR)",
      "CREATE TABLE root (id VARCHAR PRIMARY KEY, data JSON)",
      "CREATE TABLE blobs (id VARCHAR PRIMARY KEY, content BLOB NOT NULL)"
    );
    _objectsAppender = _viewerDb.CreateAppender("objects");
    _blobsAppender = _viewerDb.CreateAppender("blobs");

    _eavDb = new DuckDBConnection($"Data Source={EavDbPath}");
    _eavDb.Open();
    Execute(
      _eavDb,
      @"CREATE TABLE properties (
        object_id VARCHAR NOT NULL,
        path VARCHAR NOT NULL,
        value_text VARCHAR,
        value_num DOUBLE,
        type VARCHAR NOT NULL,
        units VARCHAR,
        internal_definition_name VARCHAR
      )"
    );
    _propsAppender = _eavDb.CreateAppender("properties");
  }

  /// <summary>
  /// Routes one serialized object into the artifact files. Mirrors the
  /// server parser's behaviour: 'reference' items are skipped, duplicate ids
  /// are written once, and the last item added is remembered as the root.
  /// </summary>
  public void Add(UploadItem item)
  {
    if (_completed)
    {
      throw new InvalidOperationException("Writer already completed.");
    }
    if (item.SpeckleType == "reference")
    {
      return;
    }

    var json = item.Json.Value;
    _lastItem = (item.Id, json);

    if (!_seen.Add(item.Id))
    {
      return;
    }

    if (item.SpeckleType == BLOB_SPECKLE_TYPE)
    {
      json = ExtractBlob(item.Id, json);
    }
    else if (EavExtraction.ProducesRows(item.SpeckleType))
    {
      // One parse serves both EAV extraction and (for DataObjects) stripping.
      var parsed = JObject.Parse(json);

      foreach (var row in EavExtraction.FlattenObjectProperties(item.Id, parsed))
      {
        AppendEavRow(row);
      }

      if (item.SpeckleType.StartsWith("Objects.Data.") && parsed.Remove("properties"))
      {
        json = parsed.ToString(Formatting.None);
      }
    }

    _objectsAppender.CreateRow().AppendValue(item.Id).AppendValue(json).AppendValue(item.SpeckleType).EndRow();
    _lastItem = (item.Id, json);
  }

  /// <summary>
  /// Stores a Blob's bytes in the blobs table (uncompressed — POC). The bytes
  /// are read from the envelope's <c>filePath</c> — the writer runs in the
  /// same process that just encoded the file, so the temp file is local.
  /// Geometry bytes deliberately do NOT travel on the v1 NDJSON wire anymore;
  /// this file is their only transport. A base64 <c>content</c> field, if some
  /// legacy envelope still carries one, is used as fallback and stripped.
  /// </summary>
  private string ExtractBlob(string id, string json)
  {
    var parsed = JObject.Parse(json);

    byte[]? bytes = null;
    var filePath = parsed["filePath"]?.Type == JTokenType.String ? (string)parsed["filePath"]! : null;
    if (filePath != null && File.Exists(filePath))
    {
      bytes = File.ReadAllBytes(filePath);
    }
    else if (parsed["content"]?.Type == JTokenType.String)
    {
      bytes = Convert.FromBase64String((string)parsed["content"]!);
    }

    if (bytes == null)
    {
      return json; // nothing to extract; envelope passes through unchanged
    }

    _blobsAppender.CreateRow().AppendValue(id).AppendValue(bytes).EndRow();

    if (parsed.Remove("content"))
    {
      return parsed.ToString(Formatting.None);
    }
    return json;
  }

  private void AppendEavRow(EavRow row) =>
    _propsAppender
      .CreateRow()
      .AppendValue(row.ObjectId)
      .AppendValue(row.Path)
      .AppendValue(row.ValueText)
      .AppendValue(row.ValueNum)
      .AppendValue(row.Type)
      .AppendValue(row.Units)
      .AppendValue(row.InternalDefinitionName)
      .EndRow();

  /// <summary>
  /// Finalizes both files: flushes appenders, writes the root row (last item
  /// added — same convention as the server, which treats the final NDJSON
  /// line as the root), creates the EAV indexes, and closes the connections.
  /// After this returns, the files are fully checkpointed and unlocked —
  /// callers can open/upload them immediately.
  /// </summary>
  public void Complete()
  {
    if (_completed)
    {
      return;
    }
    _completed = true;

    _objectsAppender.Dispose();
    _blobsAppender.Dispose();
    _propsAppender.Dispose();

    if (_lastItem is { } root)
    {
      using (var cmd = _viewerDb.CreateCommand())
      {
        cmd.CommandText = "INSERT OR IGNORE INTO root (id, data) VALUES (?, ?)";
        cmd.Parameters.Add(new DuckDBParameter(root.Id));
        cmd.Parameters.Add(new DuckDBParameter(root.Json));
        cmd.ExecuteNonQuery();
      }
    }

    Execute(_eavDb, "CREATE INDEX idx_props_path ON properties(path)", "CREATE INDEX idx_props_obj ON properties(object_id)");

    // Release DuckDB's exclusive file locks so the files can be read/uploaded
    // right away (the upload happens before this writer is disposed).
    _viewerDb.Dispose();
    _eavDb.Dispose();
  }

  public void Dispose() => Complete();

  [System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "All statements are compile-time constants (DDL); no user input."
  )]
  private static void Execute(DuckDBConnection conn, params string[] statements)
  {
    foreach (var sql in statements)
    {
      using var cmd = conn.CreateCommand();
      cmd.CommandText = sql;
      cmd.ExecuteNonQuery();
    }
  }

  private static void DeleteIfExists(string path)
  {
    if (File.Exists(path))
    {
      File.Delete(path);
    }
    // DuckDB write-ahead log from an interrupted run
    if (File.Exists(path + ".wal"))
    {
      File.Delete(path + ".wal");
    }
  }
}
#endif
