#if NET8_0_OR_GREATER
using System.Globalization;
using System.Security.Cryptography;
using Parquet;
using Parquet.Data;
using Parquet.Schema;

namespace Speckle.Sdk.Pipelines.Send.Artifacts;

/// <summary>
/// Writes the Speckle 4.0 <c>geometries.parquet</c> artifact client-side: one row per
/// mesh — <c>(geometryIndex, content, id, type)</c>. <c>geometryIndex</c> is the dense
/// geometry-namespace <c>K</c> (minted by the caller's geometry <see cref="IdInterner"/>),
/// which the envelope's <c>DISPLAY</c>/<c>DEFINES</c>/<c>HAS_MATERIAL</c> edges reference —
/// pure int, no <c>applicationId</c> strings. <c>id</c> is the SHA256 of the blob, kept as
/// a column for READ-TIME shape dedup (the server builder / viewer collapses identical
/// shapes into one GPU buffer); we do NOT content-dedup at write time, so a per-mesh row
/// stays addressable and its material bindable.
///
/// Why Parquet and not DuckDB: storing opaque mesh blobs in DuckDB drove its write engine
/// (WAL + checkpoints + transaction manager + ART index) into OOMs and an internal
/// checkpoint assertion at scale. Parquet is a passive columnar file — we append row
/// groups and close. No WAL, no checkpoints, no index build. Memory is bounded by the
/// in-flight row group (flushed on a byte budget). Consumers read it with DuckDB
/// (<c>SELECT … FROM read_parquet('…')</c>).
///
/// Written with <c>Zstd</c> compression — fast to compress and DuckDB reads it natively.
/// Not thread-safe: calls are sequential (converter loop). One row per <c>geometryIndex</c>
/// (the interner guarantees uniqueness); consumers bulk-scan or build their own index.
/// </summary>
public sealed class GeometriesParquetWriter : IDisposable
{
  // Flush (write a row group + free the buffer) once the buffered blob bytes reach
  // this budget. This is the ONLY memory knob — it bounds the in-flight row group.
  private const string ROWGROUP_MB_ENV_VAR = "SPECKLE_PARQUET_ROWGROUP_MB";
  private const int DEFAULT_ROWGROUP_MB = 64;
  private const int MAX_ROWS_PER_GROUP = 200_000; // safety cap for tiny-blob models

  // Roll to a new shard file once the CURRENT shard's uncompressed content bytes would
  // exceed this. The cap is on uncompressed blob bytes, so the on-disk (Zstd) shard is
  // always smaller than the cap — guaranteed under the viewer's per-file ceiling
  // (duckdb-wasm wasm32 32-bit file offsets / OPFS, ~4 GiB) regardless of compression
  // ratio. Shard 0 keeps the canonical `{base}.geometries.parquet` name (so a model
  // that fits in one shard is byte-for-byte unchanged); overflow shards are
  // `{base}.geometries.{N}.parquet` (N = 1, 2, …). Consumers read the set via the glob
  // `{base}.geometries*.parquet` (a single-shard model matches only the canonical name).
  // This contract is shared verbatim with the native nwextract `GeomSharder` (C++).
  private const string SHARD_MB_ENV_VAR = "SPECKLE_GEOMETRY_SHARD_MB";
  private const int DEFAULT_SHARD_MB = 1536; // 1.5 GiB uncompressed content per shard

  private const int SGEO_HEADER_SIZE = 16;
  private static ReadOnlySpan<byte> SgeoMagic => "SGEO"u8;

  /// <summary>The canonical shard-0 path (<c>{base}.geometries.parquet</c>). For the full set
  /// of shard files produced, use <see cref="GeometryPaths"/>.</summary>
  public string GeometriesPath { get; }

  /// <summary>Every geometry shard file written, in order (shard 0 = <see cref="GeometriesPath"/>).
  /// Callers that enumerate the output dir for upload pick these up automatically; this is the
  /// explicit list for callers that don't.</summary>
  public IReadOnlyList<string> GeometryPaths => _geometryPaths;

#pragma warning disable CA2213 // disposed on the background writer thread via the per-shard finalize job, not inline
  private Stream _stream = null!; // set by OpenShard(0) in the ctor
  private ParquetWriter _writer = null!;
#pragma warning restore CA2213
  private readonly DataField _indexField;
  private readonly DataField _contentField;
  private readonly DataField _idField;
  private readonly DataField _typeField;
  private readonly long _flushBytes;
  private readonly long _shardCapBytes;
  private readonly ParquetWriteScheduler _scheduler;
  private readonly HashSet<int> _seenGeometry = new();

  private readonly string _outputDir;
  private readonly string _baseName;
  private readonly ParquetSchema _schema;
  private readonly List<string> _geometryPaths = new();
  private int _shardIndex;
  private long _shardBytes; // uncompressed content bytes assigned to the current shard

  // In-flight row-group buffers (parallel lists, one entry per row).
  private readonly List<int> _indices = new();
  private readonly List<byte[]> _contents = new();
  private readonly List<string> _ids = new();
  private readonly List<string> _types = new();
  private long _bufferedBytes;
  private bool _completed;

  /// <param name="shardCapBytes">Override the per-shard uncompressed-content cap (bytes). When
  /// null, resolved from <c>SPECKLE_GEOMETRY_SHARD_MB</c> or <see cref="DEFAULT_SHARD_MB"/>. Mainly
  /// a test seam — production uses the env/default.</param>
  public GeometriesParquetWriter(
    string outputDir,
    string baseName,
    ParquetWriteScheduler scheduler,
    long? shardCapBytes = null
  )
  {
    Directory.CreateDirectory(outputDir);
    _outputDir = outputDir;
    _baseName = baseName;
    _scheduler = scheduler;
    _flushBytes = ResolveMbEnvVar(ROWGROUP_MB_ENV_VAR, DEFAULT_ROWGROUP_MB) * 1024L * 1024L;
    _shardCapBytes = shardCapBytes ?? ResolveMbEnvVar(SHARD_MB_ENV_VAR, DEFAULT_SHARD_MB) * 1024L * 1024L;

    _schema = new ParquetSchema(
      new DataField<int>("geometryIndex"),
      new DataField<byte[]>("content"),
      new DataField<string>("id"),
      new DataField<string>("type")
    );
    var fields = _schema.DataFields;
    _indexField = fields[0];
    _contentField = fields[1];
    _idField = fields[2];
    _typeField = fields[3];

    GeometriesPath = ShardPath(0);
    // A previous run may have produced MORE shards than this one will — delete the whole
    // `{base}.geometries*.parquet` set so the dir-glob upload never picks up a stale shard.
    DeleteStaleShards();
    OpenShard(0);
  }

  // Shard 0 keeps the canonical name; overflow shards get a `.{N}` ordinal before `.parquet`.
  private string ShardPath(int shardIndex) =>
    Path.Combine(
      _outputDir,
      shardIndex == 0 ? $"{_baseName}.geometries.parquet" : $"{_baseName}.geometries.{shardIndex}.parquet"
    );

  // Open a fresh shard file + ParquetWriter (Zstd). Mirrors the original ctor body; called
  // once for shard 0 and again on each roll. Synchronous create is safe — it writes the
  // parquet header to a brand-new stream, never touched by the background writer thread.
  private void OpenShard(int shardIndex)
  {
    var path = ShardPath(shardIndex);
    _stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
    _writer = ParquetWriter.CreateAsync(_schema, _stream).GetAwaiter().GetResult();
    _writer.CompressionMethod = CompressionMethod.Zstd;
    _shardBytes = 0;
    _geometryPaths.Add(path);
  }

  // Enqueue the footer-write + close of the current shard's writer/stream on the background
  // thread (FIFO after this shard's row groups), then leave _writer/_stream dangling for the
  // caller to replace (roll) or for Complete() to stop using.
  private void FinalizeCurrentShard()
  {
    var writer = _writer;
    var stream = _stream;
    _scheduler.Enqueue(() =>
    {
      writer.Dispose(); // writes the footer/metadata
      stream.Dispose();
    });
  }

  // Flush the current shard's pending rows, finalize it, and open the next one.
  private void RollShard()
  {
    FlushRowGroup();
    FinalizeCurrentShard();
    OpenShard(++_shardIndex);
  }

  private void DeleteStaleShards()
  {
    DeleteIfExists(ShardPath(0));
    foreach (var stale in Directory.EnumerateFiles(_outputDir, $"{_baseName}.geometries.*.parquet"))
    {
      DeleteIfExists(stale);
    }
  }

  /// <summary>
  /// Adds one SGEO geometry buffer under its dense <paramref name="geometryIndex"/>. The
  /// row's <c>id</c> is the SHA256 of the blob (read-time dedup column) and <c>type</c> is
  /// read from the SGEO header. A repeated <paramref name="geometryIndex"/> is written once
  /// (the caller's interner should already only emit on first sight).
  /// </summary>
  public void AddGeometry(int geometryIndex, byte[] sgeo)
  {
    if (_completed)
    {
      throw new InvalidOperationException("Writer already completed.");
    }
    if (sgeo.Length < SGEO_HEADER_SIZE || !sgeo.AsSpan(0, 4).SequenceEqual(SgeoMagic))
    {
      throw new ArgumentException("Buffer is not a valid SGEO blob.", nameof(sgeo));
    }
    if (!_seenGeometry.Add(geometryIndex))
    {
      return;
    }

    // Roll to a new shard before this blob would push the current shard past the cap.
    // Guard on _shardBytes > 0 so a single blob larger than the whole cap still lands in
    // its own shard rather than spinning up an empty file ahead of it.
    if (_shardBytes > 0 && _shardBytes + sgeo.Length > _shardCapBytes)
    {
      RollShard();
    }

    string id = Convert.ToHexString(SHA256.HashData(sgeo)).ToLowerInvariant();
    _indices.Add(geometryIndex);
    _contents.Add(sgeo);
    _ids.Add(id);
    _types.Add(PrimitiveTypeName(sgeo[5]));
    _bufferedBytes += sgeo.Length;
    _shardBytes += sgeo.Length;

    if (_bufferedBytes >= _flushBytes || _indices.Count >= MAX_ROWS_PER_GROUP)
    {
      FlushRowGroup();
    }
  }

  /// <summary>Enqueues the final row group and the file finalize (footer + close) on the background
  /// writer. The file is fully written only after <see cref="ParquetWriteScheduler.CompleteAndWait"/>.</summary>
  public void Complete()
  {
    if (_completed)
    {
      return;
    }
    _completed = true;

    FlushRowGroup();
    FinalizeCurrentShard();
  }

  public void Dispose() => Complete();

  // Snapshots the buffered rows into plain arrays and hands the encode/compress/IO to the background
  // writer (off the ODA pinned thread — see ParquetWriteScheduler). The buffer is freed immediately so
  // the producer keeps accumulating the next row group while this one is written.
  private void FlushRowGroup()
  {
    if (_indices.Count == 0)
    {
      return;
    }
    var indices = _indices.ToArray();
    var contents = _contents.ToArray();
    var ids = _ids.ToArray();
    var types = _types.ToArray();

    _indices.Clear();
    _contents.Clear();
    _ids.Clear();
    _types.Clear();
    _bufferedBytes = 0;

    var writer = _writer;
    var indexField = _indexField;
    var contentField = _contentField;
    var idField = _idField;
    var typeField = _typeField;
    _scheduler.Enqueue(() =>
    {
      using var rowGroup = writer.CreateRowGroup();
      rowGroup.WriteColumnAsync(new DataColumn(indexField, indices)).GetAwaiter().GetResult();
      rowGroup.WriteColumnAsync(new DataColumn(contentField, contents)).GetAwaiter().GetResult();
      rowGroup.WriteColumnAsync(new DataColumn(idField, ids)).GetAwaiter().GetResult();
      rowGroup.WriteColumnAsync(new DataColumn(typeField, types)).GetAwaiter().GetResult();
    });
  }

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
  }
}
#endif
