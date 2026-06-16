#if NET8_0_OR_GREATER
using System.Globalization;
using System.Security.Cryptography;
using DuckDB.NET.Data;

namespace Speckle.Sdk.Pipelines.Send.Artifacts;

/// <summary>
/// Writes the Speckle 4.0 <c>geometries.duckdb</c> artifact client-side: the
/// <c>geometries</c> table from the Integrations Board model (see
/// <c>plans/speckle-4.0/objects-duckdb-proxies-sgeo.md</c>). One row per unique
/// geometry buffer, keyed by host <c>applicationId</c>, carrying an opaque SGEO
/// blob (no JSON, no per-type columns) — and never a speckle <c>id</c> /
/// <c>__closure</c>. Topology/material relationships live in the sibling
/// <see cref="EnvelopeArtifactWriter"/> (<c>envelope.duckdb</c>).
///
/// It is a dumb sink: callers (the Objects layer / oda) encode geometry to SGEO
/// upstream and hand the bytes here. The writer derives <c>type</c> from the
/// SGEO header byte and <c>id</c> from a SHA256 of the blob (the geometry dedup
/// key — consistent with the transport's existing SHA256 blob keying).
///
/// Not thread-safe: calls are expected to be sequential (matches the converter
/// loop).
/// </summary>
public sealed class GeometriesArtifactWriter : IDisposable
{
  // Resource governance only — does not affect produced content.
  private const string MEMORY_LIMIT_MB_ENV_VAR = "SPECKLE_DUCKDB_MEMORY_LIMIT_MB";
  private const int DEFAULT_MEMORY_LIMIT_MB = 256;

  // Geometry blobs vary wildly in size (a line is ~80 B, a big mesh is MBs), so
  // the appender is recycled (committed) on whichever comes first: an accumulated
  // BYTE budget or a row count. Byte budget bounds the in-flight blob pile-up.
  private const int APPENDER_RECYCLE_INTERVAL = 25_000;
  private const string FLUSH_MB_ENV_VAR = "SPECKLE_DUCKDB_FLUSH_MB";
  private const int DEFAULT_FLUSH_MB = 64;

  private const int SGEO_HEADER_SIZE = 16;
  private static ReadOnlySpan<byte> SgeoMagic => "SGEO"u8;

  public string GeometriesDbPath { get; }

  private readonly DuckDBConnection _db;
  private readonly long _flushBytes;
  private DuckDBAppender _geometriesAppender;
  private readonly HashSet<string> _seenGeometry = new();
  private int _rowsSinceFlush;
  private long _bytesSinceFlush;
  private bool _completed;

  public GeometriesArtifactWriter(string outputDir, string baseName)
  {
    Directory.CreateDirectory(outputDir);
    GeometriesDbPath = Path.Combine(outputDir, $"{baseName}.geometries.duckdb");

    DeleteIfExists(GeometriesDbPath);

    var memoryLimitMb = ResolveMbEnvVar(MEMORY_LIMIT_MB_ENV_VAR, DEFAULT_MEMORY_LIMIT_MB);
    _flushBytes = ResolveMbEnvVar(FLUSH_MB_ENV_VAR, DEFAULT_FLUSH_MB) * 1024L * 1024L;

    _db = new DuckDBConnection($"Data Source={GeometriesDbPath}");
    _db.Open();
    Execute(
      _db,
      FormattableString.Invariant($"SET memory_limit='{memoryLimitMb}MB'"),
      "SET threads=4",
      @"CREATE TABLE geometries (
        applicationId VARCHAR NOT NULL,
        content BLOB NOT NULL,
        id VARCHAR NOT NULL,
        type VARCHAR NOT NULL,
        PRIMARY KEY (applicationId, id)
      )"
    );
    _geometriesAppender = _db.CreateAppender("geometries");
  }

  /// <summary>
  /// Adds one SGEO geometry buffer for <paramref name="applicationId"/>. The
  /// row's <c>id</c> is the SHA256 of the blob (dedup key) and <c>type</c> is
  /// read from the SGEO header. A repeated (applicationId, id) pair is written
  /// once. An object may have several rows (multi-geometry display value).
  /// </summary>
  public void AddGeometry(string applicationId, byte[] sgeo)
  {
    if (_completed)
    {
      throw new InvalidOperationException("Writer already completed.");
    }
    if (sgeo.Length < SGEO_HEADER_SIZE || !sgeo.AsSpan(0, 4).SequenceEqual(SgeoMagic))
    {
      throw new ArgumentException("Buffer is not a valid SGEO blob.", nameof(sgeo));
    }

    string id = Convert.ToHexString(SHA256.HashData(sgeo)).ToLowerInvariant();

    if (!_seenGeometry.Add($"{applicationId} {id}"))
    {
      return;
    }

    // Commit the accumulated batch on whichever fires first: byte budget (bounds
    // big-blob pile-up) or row count.
    if (_bytesSinceFlush >= _flushBytes || _rowsSinceFlush >= APPENDER_RECYCLE_INTERVAL)
    {
      RecycleAppender();
      _bytesSinceFlush = 0;
      _rowsSinceFlush = 0;
    }

    string type = PrimitiveTypeName(sgeo[5]);
    _geometriesAppender.CreateRow().AppendValue(applicationId).AppendValue(sgeo).AppendValue(id).AppendValue(type).EndRow();
    _rowsSinceFlush++;
    _bytesSinceFlush += sgeo.Length;
  }

  /// <summary>
  /// Flushes the appender, compacts the file, and closes the connection so the
  /// file can be uploaded immediately (releases the DuckDB lock).
  /// </summary>
  public void Complete()
  {
    if (_completed)
    {
      return;
    }
    _completed = true;

    _geometriesAppender.Dispose();

    // Appender recycling (which bounds write-time memory) commits many small
    // transactions against the PK-indexed geometries table; DuckDB leaves the
    // superseded blocks as free space that CHECKPOINT does NOT reclaim — only a
    // full rewrite does. So rewrite into a fresh file: the uploaded artifact is
    // then content-sized regardless of how often we recycled (or model size).
    // Observed on Navis: a 482 MB fragmented file → 265 MB compacted, same rows.
    // Runs at the loop's memory_limit (already set on the connection): the rewrite
    // streams the blobs and the geometry PK index scales with ROW COUNT (tens of
    // thousands), not blob bytes — so it needs no higher ceiling. Bumping it here
    // would inflate the finalize-phase peak (it stacks in RSS with the eav index
    // build) for no benefit.
    var compactPath = GeometriesDbPath + ".compact";
    DeleteIfExists(compactPath);
    var attach = compactPath.Replace("'", "''", StringComparison.Ordinal);
    Execute(
      _db,
      FormattableString.Invariant($"ATTACH '{attach}' AS compact"),
      @"CREATE TABLE compact.geometries (
        applicationId VARCHAR NOT NULL,
        content BLOB NOT NULL,
        id VARCHAR NOT NULL,
        type VARCHAR NOT NULL,
        PRIMARY KEY (applicationId, id)
      )",
      "INSERT INTO compact.geometries SELECT * FROM geometries",
      "CHECKPOINT compact",
      "DETACH compact"
    );
    _db.Dispose();

    // Swap the compact file in for the fragmented original.
    DeleteIfExists(GeometriesDbPath);
    File.Move(compactPath, GeometriesDbPath);
  }

  public void Dispose() => Complete();

  // SGEO primitive_type byte (header offset 0x05) → board geometries.type label.
  private static string PrimitiveTypeName(byte t) =>
    t switch
    {
      0 => "mesh",
      1 => "line",
      2 => "polyline",
      3 => "polycurve",
      4 => "curve",
      5 => "arc",
      6 => "circle",
      7 => "points",
      8 => "ellipse",
      9 => "spiral",
      10 => "box",
      _ => "unknown",
    };

  private void RecycleAppender()
  {
    _geometriesAppender.Dispose();
    _geometriesAppender = _db.CreateAppender("geometries");
  }

  [System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "Statements are compile-time constants (DDL/settings); interpolated values are a parsed int and an internally-derived, quote-escaped file path."
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
