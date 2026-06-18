#if NET8_0_OR_GREATER
using System.Globalization;
using DuckDB.NET.Data;

namespace Speckle.Sdk.Pipelines.Send.Artifacts;

/// <summary>
/// Speckle 4.0 compact, interned eav writer (Integrations Board refactor).
/// Replaces the single fat <c>eav(applicationId, path, …)</c> table with three
/// interned tables:
/// <code>
///   objects(object_index INTEGER, application_id VARCHAR)   -- one row per object
///   paths(path_index INTEGER, path VARCHAR)                 -- one row per distinct path
///   eav(object_index INTEGER, path_index INTEGER, value_*)  -- integer references only
/// </code>
/// Repeated <c>applicationId</c>s and property paths collapse to one dense int32 id
/// (in-memory interning), so eav rows carry integers instead of repeated strings —
/// far smaller — and we build only a small lookup index on
/// <c>objects.application_id</c>, NOT an ART index over the (billions-of-rows) eav
/// table (that index was the finalize-OOM cliff).
///
/// Query path (server, read-only DuckDB): resolve application_id → object_index via
/// the objects index, then join eav → paths. Not thread-safe: calls are sequential.
/// </summary>
public sealed class CompactEavWriter : IDisposable
{
  private const string MEMORY_LIMIT_MB_ENV_VAR = "SPECKLE_DUCKDB_MEMORY_LIMIT_MB";
  private const int DEFAULT_MEMORY_LIMIT_MB = 256;
  private const string INDEX_MEMORY_LIMIT_MB_ENV_VAR = "SPECKLE_DUCKDB_INDEX_MEMORY_LIMIT_MB";
  private const int DEFAULT_INDEX_MEMORY_LIMIT_MB = 1024;
  private const int APPENDER_RECYCLE_INTERVAL = 25_000;

  public string EavDbPath { get; }

  private readonly DuckDBConnection _db;
  private DuckDBAppender _objectsAppender;
  private DuckDBAppender _pathsAppender;
  private DuckDBAppender _eavAppender;

  // Interning: applicationId / path → dense sequential int32 id. The id is simply
  // the dictionary's current count, so ids are 0..N-1 in first-seen order.
  private readonly Dictionary<string, int> _objectIndex = new(StringComparer.Ordinal);
  private readonly Dictionary<string, int> _pathIndex = new(StringComparer.Ordinal);

  private int _eavRowsSinceFlush;
  private bool _completed;

  public CompactEavWriter(string outputDir, string baseName)
  {
    Directory.CreateDirectory(outputDir);
    EavDbPath = Path.Combine(outputDir, $"{baseName}.eav.duckdb");
    DeleteIfExists(EavDbPath);

    var memoryLimitMb = ResolveMbEnvVar(MEMORY_LIMIT_MB_ENV_VAR, DEFAULT_MEMORY_LIMIT_MB);
    _db = new DuckDBConnection($"Data Source={EavDbPath}");
    _db.Open();
    Execute(
      _db,
      FormattableString.Invariant($"SET memory_limit='{memoryLimitMb}MB'"),
      "SET threads=4",
      "CREATE TABLE objects (object_index INTEGER NOT NULL, application_id VARCHAR NOT NULL)",
      "CREATE TABLE paths (path_index INTEGER NOT NULL, path VARCHAR NOT NULL)",
      @"CREATE TABLE eav (
        object_index INTEGER NOT NULL,
        path_index INTEGER NOT NULL,
        value_string VARCHAR,
        value_double DOUBLE,
        value_boolean BOOLEAN,
        unit VARCHAR,
        internal_definition_name VARCHAR
      )"
    );
    _objectsAppender = _db.CreateAppender("objects");
    _pathsAppender = _db.CreateAppender("paths");
    _eavAppender = _db.CreateAppender("eav");
  }

  /// <summary>
  /// Appends the flattened rows for one object, keyed by its
  /// <paramref name="applicationId"/>. The applicationId is interned to an
  /// <c>object_index</c> and each <see cref="EavRow.Path"/> to a <c>path_index</c>;
  /// the eav rows store only those integer references plus the values.
  /// </summary>
  public void AddRows(string applicationId, IEnumerable<EavRow> rows)
  {
    if (_completed)
    {
      throw new InvalidOperationException("Writer already completed.");
    }

    int objectIndex = GetOrAddObject(applicationId);
    foreach (var row in rows)
    {
      int pathIndex = GetOrAddPath(row.Path);
      bool? valueBoolean = null;
      if (row.Type == "boolean" && bool.TryParse(row.ValueText, out var b))
      {
        valueBoolean = b;
      }

      _eavAppender
        .CreateRow()
        .AppendValue(objectIndex)
        .AppendValue(pathIndex)
        .AppendValue(row.ValueText)
        .AppendValue(row.ValueNum)
        .AppendValue(valueBoolean)
        .AppendValue(row.Units)
        .AppendValue(row.InternalDefinitionName)
        .EndRow();

      if (++_eavRowsSinceFlush >= APPENDER_RECYCLE_INTERVAL)
      {
        RecycleAppenders();
        _eavRowsSinceFlush = 0;
      }
    }
  }

  /// <summary>Flushes, builds the small objects lookup index, and closes the file.</summary>
  public void Complete()
  {
    if (_completed)
    {
      return;
    }
    _completed = true;

    _objectsAppender.Dispose();
    _pathsAppender.Dispose();
    _eavAppender.Dispose();

    // Only the per-object lookup index (objects.application_id) — one row per
    // object, small. Deliberately NO index over eav: an ART index across the
    // hundreds-of-millions / billions of eav rows is the finalize-OOM cliff, and
    // consumers query eav by joining on the interned integer ids, not by string.
    Execute(
      _db,
      FormattableString.Invariant(
        $"SET memory_limit='{ResolveMbEnvVar(INDEX_MEMORY_LIMIT_MB_ENV_VAR, DEFAULT_INDEX_MEMORY_LIMIT_MB)}MB'"
      )
    );
    Execute(_db, "CREATE INDEX idx_objects_appid ON objects(application_id)");

    _db.Dispose();
  }

  /// <summary>
  /// Cleanup path (e.g. the owning <c>using</c> firing during exception unwind) —
  /// stays cheap and never throws: it does NOT build the index here (the heaviest
  /// step), which on a failing run would waste work and mask the real exception.
  /// </summary>
  public void Dispose()
  {
    if (_completed)
    {
      return;
    }
    _completed = true;
    try
    {
      _objectsAppender.Dispose();
      _pathsAppender.Dispose();
      _eavAppender.Dispose();
      _db.Dispose();
    }
#pragma warning disable CA1031 // cleanup path: swallow so the original failure propagates unmasked
    catch (Exception)
#pragma warning restore CA1031
    {
      // Intentionally ignored.
    }
  }

  private int GetOrAddObject(string applicationId)
  {
    if (_objectIndex.TryGetValue(applicationId, out var idx))
    {
      return idx;
    }
    idx = _objectIndex.Count;
    _objectIndex.Add(applicationId, idx);
    _objectsAppender.CreateRow().AppendValue(idx).AppendValue(applicationId).EndRow();
    return idx;
  }

  private int GetOrAddPath(string path)
  {
    if (_pathIndex.TryGetValue(path, out var idx))
    {
      return idx;
    }
    idx = _pathIndex.Count;
    _pathIndex.Add(path, idx);
    _pathsAppender.CreateRow().AppendValue(idx).AppendValue(path).EndRow();
    return idx;
  }

  private void RecycleAppenders()
  {
    _objectsAppender.Dispose();
    _pathsAppender.Dispose();
    _eavAppender.Dispose();
    _objectsAppender = _db.CreateAppender("objects");
    _pathsAppender = _db.CreateAppender("paths");
    _eavAppender = _db.CreateAppender("eav");
  }

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

  private static int ResolveMbEnvVar(string name, int fallback)
  {
    var raw = Environment.GetEnvironmentVariable(name);
    return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mb) && mb > 0 ? mb : fallback;
  }

  private static void DeleteIfExists(string path)
  {
    if (File.Exists(path))
    {
      File.Delete(path);
    }
    if (File.Exists(path + ".wal"))
    {
      File.Delete(path + ".wal");
    }
  }
}
#endif
