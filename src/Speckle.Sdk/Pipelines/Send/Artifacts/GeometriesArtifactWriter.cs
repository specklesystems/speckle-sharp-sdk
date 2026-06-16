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

  // Hard floor for the GEOMETRY connection's buffer pool. Unlike the eav connection
  // (tiny uniform rows), this one commits whole mesh blobs — a single mesh can be
  // tens of MB, and DuckDB must allocate a contiguous block for it on top of the
  // in-flight batch. If the shared memory_limit knob is tuned below this (e.g. 64
  // to shave the loop peak), a big-mesh model OOMs on commit. The pool MUST exceed
  // the largest blob + working room, so we clamp up regardless of the shared knob.
  // (The ~200 MB this costs vs a tiny pool is noise next to ODA's ~1.3 GB native
  // floor — starving it below a single mesh is a false economy.)
  private const int MIN_MEMORY_LIMIT_MB = 256;

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

    var memoryLimitMb = Math.Max(ResolveMbEnvVar(MEMORY_LIMIT_MB_ENV_VAR, DEFAULT_MEMORY_LIMIT_MB), MIN_MEMORY_LIMIT_MB);
    _flushBytes = ResolveMbEnvVar(FLUSH_MB_ENV_VAR, DEFAULT_FLUSH_MB) * 1024L * 1024L;

    _db = new DuckDBConnection($"Data Source={GeometriesDbPath}");
    _db.Open();
    Execute(
      _db,
      FormattableString.Invariant($"SET memory_limit='{memoryLimitMb}MB'"),
      "SET threads=4",
      // No PRIMARY KEY: uniqueness of (applicationId, id) is already guaranteed by
      // the in-memory _seenGeometry dedup, and consumers bulk-scan geometry (no
      // point lookup needs the index). Dropping it removes the ART-index
      // maintenance that fragmented the file under appender recycling (which is
      // why we no longer rewrite/compact at finalize) AND removes the index's
      // memory cost — so committing a big mesh blob only needs room for the blob,
      // not blob + index.
      @"CREATE TABLE geometries (
        applicationId VARCHAR NOT NULL,
        content BLOB NOT NULL,
        id VARCHAR NOT NULL,
        type VARCHAR NOT NULL
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

    // Commit (recycle) the accumulated batch BEFORE appending this blob if adding
    // it would push the batch over the byte budget — i.e. empty the desk first, so
    // a large blob commits in (close to) its own batch instead of lumped on top of
    // an accumulated one. That keeps each commit's peak ≈ one blob rather than
    // batch+blob, which is what made big-mesh commits OOM. The row-count interval
    // is the other trigger. (Guarded on a non-empty batch so we never recycle an
    // empty appender; a single blob bigger than the whole pool still can't be
    // committed — the pool must exceed the biggest mesh — but this raises the
    // safe single-mesh size to ~the full pool instead of pool-minus-batch.)
    bool wouldOverflowBudget = _bytesSinceFlush + sgeo.Length >= _flushBytes;
    if (_rowsSinceFlush > 0 && (wouldOverflowBudget || _rowsSinceFlush >= APPENDER_RECYCLE_INTERVAL))
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
  /// Flushes the appender, checkpoints, and closes the connection so the file can
  /// be uploaded immediately (releases the DuckDB lock).
  /// </summary>
  public void Complete()
  {
    if (_completed)
    {
      return;
    }
    _completed = true;

    // No finalize rewrite/compaction. It existed only to reclaim the free blocks
    // left by appender recycling on a PK-indexed table — but it had to buffer
    // whole mesh blobs to rewrite them, so a single large mesh OOM'd it (and a
    // failed rewrite fatally invalidated the DB). With the PK dropped the table is
    // append-only, so recycling no longer fragments meaningfully and there is
    // nothing to reclaim. A plain checkpoint is all that's needed.
    _geometriesAppender.Dispose();
    Execute(_db, "CHECKPOINT");
    _db.Dispose();
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
