#if NET8_0_OR_GREATER
using System.Globalization;
using DuckDB.NET.Data;

namespace Speckle.Sdk.Pipelines.Send.Artifacts;

/// <summary>
/// Writes the Speckle 4.0 <c>envelope.duckdb</c> artifact client-side: the
/// <c>proxies</c> table from the Integrations Board model (see
/// <c>plans/speckle-4.0/objects-duckdb-proxies-sgeo.md</c>). This is the lean
/// topology file — relationships between objects (instanceDef | layer | material
/// | colour | group | level, …) rather than a single object's geometry. It
/// replaces the collection/object tree ("no collection, all topology via
/// proxies"). Geometry blobs live in the sibling
/// <see cref="GeometriesParquetWriter"/> (<c>geometries.parquet</c>); flattened
/// properties live in <see cref="EavWriter"/> (<c>eav.duckdb</c>).
///
/// Proxies are typically written last, after all geometry, in one tight pass.
/// Not thread-safe: calls are expected to be sequential.
/// </summary>
public sealed class EnvelopeWriter : IDisposable
{
  // Resource governance only — does not affect produced content.
  private const string MEMORY_LIMIT_MB_ENV_VAR = "SPECKLE_DUCKDB_MEMORY_LIMIT_MB";
  private const int DEFAULT_MEMORY_LIMIT_MB = 256;
  private const int APPENDER_RECYCLE_INTERVAL = 10_000;

  public string EnvelopeDbPath { get; }

  private readonly DuckDBConnection _db;
  private DuckDBAppender _proxiesAppender;
  private int _rowCount;
  private bool _completed;

  public EnvelopeWriter(string outputDir, string baseName)
  {
    Directory.CreateDirectory(outputDir);
    EnvelopeDbPath = Path.Combine(outputDir, $"{baseName}.envelope.duckdb");

    DeleteIfExists(EnvelopeDbPath);

    var memoryLimitMb = ResolveMbEnvVar(MEMORY_LIMIT_MB_ENV_VAR, DEFAULT_MEMORY_LIMIT_MB);
    _db = new DuckDBConnection($"Data Source={EnvelopeDbPath}");
    _db.Open();
    Execute(
      _db,
      FormattableString.Invariant($"SET memory_limit='{memoryLimitMb}MB'"),
      "SET threads=4",
      "CREATE TABLE proxies (type VARCHAR NOT NULL, data JSON NOT NULL)"
    );
    _proxiesAppender = _db.CreateAppender("proxies");
  }

  /// <summary>
  /// Adds one proxy row. <paramref name="type"/> is one of the board's proxy
  /// kinds (instanceDef | layer | material | colour | group | level);
  /// <paramref name="dataJson"/> is the per-type JSON envelope.
  /// </summary>
  public void AddProxy(string type, string dataJson)
  {
    if (_completed)
    {
      throw new InvalidOperationException("Writer already completed.");
    }
    _proxiesAppender.CreateRow().AppendValue(type).AppendValue(dataJson).EndRow();
    if (++_rowCount % APPENDER_RECYCLE_INTERVAL == 0)
    {
      RecycleAppender();
    }
  }

  /// <summary>
  /// Flushes the appender, checkpoints, and closes the connection so the file
  /// can be uploaded immediately (releases the DuckDB lock).
  /// </summary>
  public void Complete()
  {
    if (_completed)
    {
      return;
    }
    _completed = true;

    // Disposing the appender flushes the final batch; disposing the connection
    // checkpoints the WAL into the file. No explicit CHECKPOINT — it overlaps the
    // appender's dispose-flush checkpoint and can trip a DuckDB internal assertion
    // ("active_checkpoint was already set"). Let close own the single checkpoint.
    _proxiesAppender.Dispose();
    _db.Dispose();
  }

  public void Dispose() => Complete();

  private void RecycleAppender()
  {
    _proxiesAppender.Dispose();
    _proxiesAppender = _db.CreateAppender("proxies");
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
