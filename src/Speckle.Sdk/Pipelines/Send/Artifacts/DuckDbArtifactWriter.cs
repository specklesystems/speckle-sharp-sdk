#if NET8_0_OR_GREATER
using System.Globalization;
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
/// <item><c>{baseName}.viewer.duckdb</c> — same table shape as today's server
/// packfile (objects / root / blobs), with two reductions:
/// DataObject envelopes have <c>properties</c> stripped (they live in eav),
/// and Blob bytes are read from the envelope's local <c>filePath</c> into the
/// <c>blobs</c> table (uncompressed for now — POC keeps C# simple). Geometry
/// bytes do NOT travel on the v1 NDJSON wire — this file is their only home.</item>
/// <item><c>{baseName}.eav.duckdb</c> — the <c>properties</c> table, produced
/// by <see cref="EavExtraction"/> (client-side port of the server's
/// flattenObjectProperties).</item>
/// </list>
///
/// Not thread-safe: <see cref="Add(string, string, string)"/> is expected to
/// be called sequentially (matches SendPipeline.Process usage).
/// </summary>
public sealed class DuckDbArtifactWriter : IDisposable
{
  private const string BLOB_SPECKLE_TYPE = "Speckle.Core.Models.Blob";

  // Resource governance only — none of these affect the produced content
  // (same rows, same EAV, same indexes; insertion order preserved).

  /// <summary>
  /// Caps each DuckDB instance's buffer pool (viewer + eav are separate
  /// files, so the DuckDB budget is 2× this during appends). Without a limit
  /// DuckDB defaults to 80% of machine RAM and keeps every written block
  /// cached until Complete() — the whole dataset rides in RAM.
  /// </summary>
  private const string MEMORY_LIMIT_MB_ENV_VAR = "SPECKLE_DUCKDB_MEMORY_LIMIT_MB";
  private const int DEFAULT_MEMORY_LIMIT_MB = 256;

  /// <summary>
  /// CREATE INDEX over tens of millions of rows needs more than the append
  /// budget (the ART index is built and checkpointed in memory) — the eav
  /// connection's limit is raised to this for the index phase only, after
  /// the viewer connection has closed and released its budget. 512MB OOMs on
  /// a 29M-row table.
  /// </summary>
  private const string INDEX_MEMORY_LIMIT_MB_ENV_VAR = "SPECKLE_DUCKDB_INDEX_MEMORY_LIMIT_MB";
  private const int DEFAULT_INDEX_MEMORY_LIMIT_MB = 1024;

  /// <summary>
  /// Appenders defer persistence until disposed; recycling them every N
  /// objects commits the accumulated rows so DuckDB can checkpoint them to
  /// the file and evict the blocks mid-run.
  /// </summary>
  private const int APPENDER_RECYCLE_INTERVAL = 25_000;

  public string ViewerDbPath { get; }
  public string EavDbPath { get; }

  private readonly DuckDBConnection _viewerDb;
  private readonly DuckDBConnection _eavDb;
  private DuckDBAppender _objectsAppender;
  private DuckDBAppender _blobsAppender;
  private DuckDBAppender _propsAppender;
  private readonly HashSet<string> _seen = new();
  private (string Id, string Json)? _lastItem;
  private bool _completed;

  public DuckDbArtifactWriter(string outputDir, string baseName)
  {
    Directory.CreateDirectory(outputDir);
    ViewerDbPath = Path.Combine(outputDir, $"{baseName}.viewer.duckdb");
    EavDbPath = Path.Combine(outputDir, $"{baseName}.eav.duckdb");

    // Stale files from a previous run make CREATE TABLE fail.
    DeleteIfExists(ViewerDbPath);
    DeleteIfExists(EavDbPath);

    var memoryLimitMb = ResolveMbEnvVar(MEMORY_LIMIT_MB_ENV_VAR, DEFAULT_MEMORY_LIMIT_MB);

    _viewerDb = new DuckDBConnection($"Data Source={ViewerDbPath}");
    _viewerDb.Open();
    Execute(
      _viewerDb,
      FormattableString.Invariant($"SET memory_limit='{memoryLimitMb}MB'"),
      "SET threads=4", // fewer threads = smaller checkpoint/compression buffers
      "CREATE TABLE objects (id VARCHAR PRIMARY KEY, data JSON, speckle_type VARCHAR)",
      "CREATE TABLE root (id VARCHAR PRIMARY KEY, data JSON)",
      "CREATE TABLE blobs (id VARCHAR PRIMARY KEY, content BLOB NOT NULL)"
    );
    _objectsAppender = _viewerDb.CreateAppender("objects");
    _blobsAppender = _viewerDb.CreateAppender("blobs");

    MemoryLog.Log("writer: viewer db opened");

    _eavDb = new DuckDBConnection($"Data Source={EavDbPath}");
    _eavDb.Open();
    Execute(
      _eavDb,
      FormattableString.Invariant($"SET memory_limit='{memoryLimitMb}MB'"),
      "SET threads=4",
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

  public void Add(UploadItem item) => Add(item.Id, item.SpeckleType, item.Json.Value);

  /// <summary>
  /// Routes one serialized object into the artifact files. Mirrors the
  /// server parser's behaviour: 'reference' items are skipped, duplicate ids
  /// are written once, and the last item added is remembered as the root.
  /// </summary>
  public void Add(string id, string speckleType, string json)
  {
    if (_completed)
    {
      throw new InvalidOperationException("Writer already completed.");
    }
    if (speckleType == "reference")
    {
      return;
    }

    _lastItem = (id, json);

    if (!_seen.Add(id))
    {
      return;
    }

    if (_seen.Count % APPENDER_RECYCLE_INTERVAL == 0)
    {
      RecycleAppenders();
      MemoryLog.Log($"writer: {_seen.Count} objects added (appenders recycled)");
    }

    if (speckleType == BLOB_SPECKLE_TYPE)
    {
      json = ExtractBlob(id, json);
    }
    else if (EavExtraction.ProducesRows(speckleType))
    {
      // One parse serves both EAV extraction and (for DataObjects) stripping.
      var parsed = JObject.Parse(json);

      foreach (var row in EavExtraction.FlattenObjectProperties(id, parsed))
      {
        AppendEavRow(row);
      }

      if (speckleType.StartsWith("Objects.Data.") && parsed.Remove("properties"))
      {
        json = parsed.ToString(Formatting.None);
      }
    }

    _objectsAppender.CreateRow().AppendValue(id).AppendValue(json).AppendValue(speckleType).EndRow();
    _lastItem = (id, json);
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

    // The giant collection/root envelopes arrive LAST (root-last convention),
    // so their parse garbage (hundreds of MB of dead DOM/strings) is freshly
    // committed right before the heaviest phases — decommit it before the
    // flush + index builds stack on top. Aggressive mode (net7+) compacts AND
    // returns memory to the OS.
    GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
    MemoryLog.Log("writer: aggressive GC done");

    MemoryLog.Phase($"writer: appender flush ({_seen.Count} objects)");
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

    // The viewer file is finished — close it BEFORE the index builds so its
    // entire buffer-pool budget is released ahead of the heaviest phase.
    // (Closing also releases the file lock; the upload happens later.)
    MemoryLog.Phase("writer: viewer checkpoint + close");
    _viewerDb.Dispose();

    MemoryLog.Phase("writer: index(path) build");
    // Index builds need more than the append budget — raise the limit for
    // this phase only (the connection closes right after).
    Execute(
      _eavDb,
      FormattableString.Invariant(
        $"SET memory_limit='{ResolveMbEnvVar(INDEX_MEMORY_LIMIT_MB_ENV_VAR, DEFAULT_INDEX_MEMORY_LIMIT_MB)}MB'"
      )
    );
    Execute(_eavDb, "CREATE INDEX idx_props_path ON properties(path)");

    MemoryLog.Phase("writer: index(object_id) build");
    Execute(_eavDb, "CREATE INDEX idx_props_obj ON properties(object_id)");

    MemoryLog.Phase("writer: eav checkpoint + close");
    _eavDb.Dispose();
  }

  public void Dispose() => Complete();

  [System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "Statements are compile-time constants (DDL/settings); the only interpolated value is a parsed int."
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

  /// <summary>
  /// Disposing an appender commits its accumulated rows, which makes them
  /// checkpointable to the file and evictable from the buffer pool mid-run.
  /// </summary>
  private void RecycleAppenders()
  {
    _objectsAppender.Dispose();
    _blobsAppender.Dispose();
    _propsAppender.Dispose();
    _objectsAppender = _viewerDb.CreateAppender("objects");
    _blobsAppender = _viewerDb.CreateAppender("blobs");
    _propsAppender = _eavDb.CreateAppender("properties");
  }

  private static int ResolveMbEnvVar(string name, int fallback)
  {
    var raw = Environment.GetEnvironmentVariable(name);
    return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mb) && mb > 0
      ? mb
      : fallback;
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
