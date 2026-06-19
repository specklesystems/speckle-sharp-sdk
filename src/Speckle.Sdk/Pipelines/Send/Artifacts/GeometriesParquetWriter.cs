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

  private const int SGEO_HEADER_SIZE = 16;
  private static ReadOnlySpan<byte> SgeoMagic => "SGEO"u8;

  public string GeometriesPath { get; }

  private readonly Stream _stream;
  private readonly ParquetWriter _writer;
  private readonly DataField _indexField;
  private readonly DataField _contentField;
  private readonly DataField _idField;
  private readonly DataField _typeField;
  private readonly long _flushBytes;
  private readonly HashSet<int> _seenGeometry = new();

  // In-flight row-group buffers (parallel lists, one entry per row).
  private readonly List<int> _indices = new();
  private readonly List<byte[]> _contents = new();
  private readonly List<string> _ids = new();
  private readonly List<string> _types = new();
  private long _bufferedBytes;
  private bool _completed;

  public GeometriesParquetWriter(string outputDir, string baseName)
  {
    Directory.CreateDirectory(outputDir);
    GeometriesPath = Path.Combine(outputDir, $"{baseName}.geometries.parquet");
    DeleteIfExists(GeometriesPath);

    _flushBytes = ResolveMbEnvVar(ROWGROUP_MB_ENV_VAR, DEFAULT_ROWGROUP_MB) * 1024L * 1024L;

    var schema = new ParquetSchema(
      new DataField<int>("geometryIndex"),
      new DataField<byte[]>("content"),
      new DataField<string>("id"),
      new DataField<string>("type")
    );
    var fields = schema.DataFields;
    _indexField = fields[0];
    _contentField = fields[1];
    _idField = fields[2];
    _typeField = fields[3];

    _stream = new FileStream(GeometriesPath, FileMode.Create, FileAccess.Write, FileShare.None);
    _writer = ParquetWriter.CreateAsync(schema, _stream).GetAwaiter().GetResult();
    _writer.CompressionMethod = CompressionMethod.Zstd;
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

    string id = Convert.ToHexString(SHA256.HashData(sgeo)).ToLowerInvariant();
    _indices.Add(geometryIndex);
    _contents.Add(sgeo);
    _ids.Add(id);
    _types.Add(PrimitiveTypeName(sgeo[5]));
    _bufferedBytes += sgeo.Length;

    if (_bufferedBytes >= _flushBytes || _indices.Count >= MAX_ROWS_PER_GROUP)
    {
      FlushRowGroup();
    }
  }

  /// <summary>Writes the final row group and the Parquet footer, then closes the file.</summary>
  public void Complete()
  {
    if (_completed)
    {
      return;
    }
    _completed = true;

    FlushRowGroup();
    _writer.Dispose(); // writes the footer/metadata
    _stream.Dispose();
  }

  public void Dispose() => Complete();

  // Serializes the buffered rows as one Parquet row group and frees the buffers.
  private void FlushRowGroup()
  {
    if (_indices.Count == 0)
    {
      return;
    }
    using var rowGroup = _writer.CreateRowGroup();
    rowGroup.WriteColumnAsync(new DataColumn(_indexField, _indices.ToArray())).GetAwaiter().GetResult();
    rowGroup.WriteColumnAsync(new DataColumn(_contentField, _contents.ToArray())).GetAwaiter().GetResult();
    rowGroup.WriteColumnAsync(new DataColumn(_idField, _ids.ToArray())).GetAwaiter().GetResult();
    rowGroup.WriteColumnAsync(new DataColumn(_typeField, _types.ToArray())).GetAwaiter().GetResult();

    _indices.Clear();
    _contents.Clear();
    _ids.Clear();
    _types.Clear();
    _bufferedBytes = 0;
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
