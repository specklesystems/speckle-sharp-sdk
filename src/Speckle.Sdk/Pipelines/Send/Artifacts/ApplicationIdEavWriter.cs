#if NET8_0_OR_GREATER
using System.Globalization;
using DuckDB.NET.Data;

namespace Speckle.Sdk.Pipelines.Send.Artifacts;

/// <summary>
/// Writes the Speckle 4.0 full-binary <c>eav.duckdb</c> — distinct from the
/// envelope path's <see cref="DuckDbArtifactWriter"/> <c>properties</c> table in
/// two ways: rows are keyed by host <c>applicationId</c> (not the speckle object
/// <c>id</c>, which doesn't exist in the binary path), and the value columns
/// follow the Integrations Board schema
/// (<c>value_string</c>/<c>value_double</c>/<c>value_boolean</c>). Flattening is
/// still done by the shared <see cref="EavExtraction"/>; only the key and the
/// column shape differ.
///
/// Not thread-safe: calls are expected to be sequential (converter loop).
/// </summary>
public sealed class ApplicationIdEavWriter : IDisposable
{
  private const string MEMORY_LIMIT_MB_ENV_VAR = "SPECKLE_DUCKDB_MEMORY_LIMIT_MB";
  private const int DEFAULT_MEMORY_LIMIT_MB = 256;
  private const string INDEX_MEMORY_LIMIT_MB_ENV_VAR = "SPECKLE_DUCKDB_INDEX_MEMORY_LIMIT_MB";
  private const int DEFAULT_INDEX_MEMORY_LIMIT_MB = 1024;
  private const int APPENDER_RECYCLE_INTERVAL = 10_000;

  public string EavDbPath { get; }

  private readonly DuckDBConnection _db;
  private DuckDBAppender _appender;
  private int _rowCount;
  private bool _completed;

  public ApplicationIdEavWriter(string outputDir, string baseName)
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
      @"CREATE TABLE eav (
        applicationId VARCHAR NOT NULL,
        path VARCHAR NOT NULL,
        value_string VARCHAR,
        value_double DOUBLE,
        value_boolean BOOLEAN,
        unit VARCHAR,
        internal_definition_name VARCHAR
      )"
    );
    _appender = _db.CreateAppender("eav");
  }

  /// <summary>
  /// Appends the flattened rows for one object, keyed by its
  /// <paramref name="applicationId"/>. <paramref name="rows"/> come from
  /// <see cref="EavExtraction.FlattenObjectProperties"/> (the <c>ObjectId</c>
  /// field is ignored — the application id is the key here).
  /// </summary>
  public void AddRows(string applicationId, IEnumerable<EavRow> rows)
  {
    if (_completed)
    {
      throw new InvalidOperationException("Writer already completed.");
    }
    foreach (var row in rows)
    {
      bool? valueBoolean = null;
      if (row.Type == "boolean" && bool.TryParse(row.ValueText, out var b))
      {
        valueBoolean = b;
      }

      _appender
        .CreateRow()
        .AppendValue(applicationId)
        .AppendValue(row.Path)
        .AppendValue(row.ValueText)
        .AppendValue(row.ValueNum)
        .AppendValue(valueBoolean)
        .AppendValue(row.Units)
        .AppendValue(row.InternalDefinitionName)
        .EndRow();

      if (++_rowCount % APPENDER_RECYCLE_INTERVAL == 0)
      {
        RecycleAppender();
      }
    }
  }

  /// <summary>Flushes, builds the per-object lookup index, and closes the file.</summary>
  public void Complete()
  {
    if (_completed)
    {
      return;
    }
    _completed = true;

    _appender.Dispose();

    // Only the applicationId (per-object) index — matching the agreed logic for
    // the envelope writer (drop the path index, keep the object index). The
    // dominant query is "all properties for this object"; a global path index
    // roughly doubles index-build memory/time at tens of millions of rows for a
    // pattern we don't rely on. Raise the limit for the build phase only.
    Execute(
      _db,
      FormattableString.Invariant(
        $"SET memory_limit='{ResolveMbEnvVar(INDEX_MEMORY_LIMIT_MB_ENV_VAR, DEFAULT_INDEX_MEMORY_LIMIT_MB)}MB'"
      )
    );
    Execute(_db, "CREATE INDEX idx_eav_appid ON eav(applicationId)");

    _db.Dispose();
  }

  /// <summary>
  /// Cleanup path (e.g. the owning <c>using</c> firing during exception unwind) —
  /// deliberately NOT the success path. It stays cheap and never throws: building
  /// the index here (the heaviest, most OOM-prone step) on a failing run would both
  /// waste work and MASK the real exception that triggered the unwind — a throwing
  /// Dispose replaces the original exception in a try/finally. The index is built
  /// only by the explicit <see cref="Complete"/>; cleanup just releases the handles.
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
      _appender.Dispose();
      _db.Dispose();
    }
#pragma warning disable CA1031 // cleanup path: swallow so the original failure propagates unmasked
    catch (Exception)
#pragma warning restore CA1031
    {
      // Intentionally ignored.
    }
  }

  private void RecycleAppender()
  {
    _appender.Dispose();
    _appender = _db.CreateAppender("eav");
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
