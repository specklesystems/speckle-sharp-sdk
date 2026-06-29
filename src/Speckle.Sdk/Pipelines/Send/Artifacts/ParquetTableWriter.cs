#if NETSTANDARD2_0 || NET8_0_OR_GREATER
using Parquet;
using Parquet.Data;
using Parquet.Schema;

namespace Speckle.Sdk.Pipelines.Send.Artifacts;

/// <summary>
/// Generic columnar Parquet table writer (Zstd), row-group-buffered exactly like
/// <see cref="GeometriesParquetWriter"/> — a passive columnar file: append row groups and close, NO
/// WAL/checkpoint/transaction manager/index. Memory is bounded by the in-flight row group (flushed on a
/// row budget), so it scales to arbitrary row counts at constant memory. DuckDB reads it natively
/// (<c>read_parquet('…')</c>).
///
/// Rows are added as <c>object?[]</c> in schema-column order; nullable value columns accept null.
/// Supported column types: int / int? / double / double? / bool / bool? / string. Not thread-safe:
/// calls are sequential (converter loop).
/// </summary>
public sealed class ParquetTableWriter : IDisposable
{
  private const int DEFAULT_ROWGROUP_ROWS = 200_000;

  public string Path { get; }

#pragma warning disable CA2213 // disposed on the background writer thread via the Complete() finalize job, not inline
  private readonly Stream _stream;
  private readonly ParquetWriter _writer;
#pragma warning restore CA2213
  private readonly DataField[] _fields;
  private readonly Col[] _cols;
  private readonly int _flushRows;
  private readonly ParquetWriteScheduler _scheduler;
  private int _buffered;
  private bool _completed;

  public ParquetTableWriter(
    string path,
    ParquetSchema schema,
    ParquetWriteScheduler scheduler,
    int flushRows = DEFAULT_ROWGROUP_ROWS
  )
  {
    Path = path;
    DeleteIfExists(path);

    _scheduler = scheduler;
    _fields = schema.DataFields;
    _cols = new Col[_fields.Length];
    for (var i = 0; i < _fields.Length; i++)
    {
      _cols[i] = MakeCol(_fields[i]);
    }
    _flushRows = flushRows;

    _stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
    _writer = ParquetWriter.CreateAsync(schema, _stream).GetAwaiter().GetResult();
    _writer.CompressionMethod = CompressionMethod.Zstd;
  }

  /// <summary>Appends one row; <paramref name="values"/> are in schema-column order.</summary>
  public void AddRow(params object?[] values)
  {
    if (_completed)
    {
      throw new InvalidOperationException("Writer already completed.");
    }
    for (var i = 0; i < _cols.Length; i++)
    {
      _cols[i].Add(values[i]);
    }
    if (++_buffered >= _flushRows)
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

    // Footer/close runs on the background thread too, AFTER this file's row-group jobs (FIFO),
    // so it never blocks the producer and never races the row-group writes.
    var writer = _writer;
    var stream = _stream;
    _scheduler.Enqueue(() =>
    {
      writer.Dispose(); // footer/metadata
      stream.Dispose();
    });
  }

  public void Dispose() => Complete();

  // Snapshots the buffered columns into plain arrays and hands the encode/compress/IO to the
  // background writer. The producer keeps buffering the next row group immediately; the only thing
  // that ever touches _writer/_stream after construction is the background thread.
  private void FlushRowGroup()
  {
    if (_buffered == 0)
    {
      return;
    }
    var arrays = new Array[_fields.Length];
    for (var i = 0; i < _fields.Length; i++)
    {
      arrays[i] = _cols[i].ToArrayAndClear();
    }
    _buffered = 0;

    var writer = _writer;
    var fields = _fields;
    _scheduler.Enqueue(() =>
    {
      using var rowGroup = writer.CreateRowGroup();
      for (var i = 0; i < fields.Length; i++)
      {
        rowGroup.WriteColumnAsync(new DataColumn(fields[i], arrays[i])).GetAwaiter().GetResult();
      }
    });
  }

  private static Col MakeCol(DataField f)
  {
    var t = Nullable.GetUnderlyingType(f.ClrType) ?? f.ClrType;
    var nullable = f.IsNullable || Nullable.GetUnderlyingType(f.ClrType) != null;

    if (t == typeof(int))
    {
      return nullable ? new Col<int?>() : new Col<int>();
    }
    if (t == typeof(double))
    {
      return nullable ? new Col<double?>() : new Col<double>();
    }
    if (t == typeof(bool))
    {
      return nullable ? new Col<bool?>() : new Col<bool>();
    }
    if (t == typeof(string))
    {
      return new Col<string?>(); // parquet strings are nullable
    }
    throw new NotSupportedException($"ParquetTableWriter: unsupported column type {f.ClrType} for '{f.Name}'.");
  }

  private static void DeleteIfExists(string path)
  {
    if (File.Exists(path))
    {
      File.Delete(path);
    }
  }

  private abstract class Col
  {
    public abstract void Add(object? v);
    public abstract Array ToArrayAndClear();
  }

  private sealed class Col<T> : Col
  {
    private List<T> _list = new();

    public override void Add(object? v) => _list.Add(v is null ? default! : (T)v);

    public override Array ToArrayAndClear()
    {
      var arr = _list.ToArray();
      _list = new List<T>();
      return arr;
    }
  }
}
#endif
