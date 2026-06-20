#if NET8_0_OR_GREATER
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

  private readonly Stream _stream;
  private readonly ParquetWriter _writer;
  private readonly DataField[] _fields;
  private readonly Col[] _cols;
  private readonly int _flushRows;
  private int _buffered;
  private bool _completed;

  public ParquetTableWriter(string path, ParquetSchema schema, int flushRows = DEFAULT_ROWGROUP_ROWS)
  {
    Path = path;
    DeleteIfExists(path);

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

  /// <summary>Writes the final row group and the Parquet footer, then closes the file.</summary>
  public void Complete()
  {
    if (_completed)
    {
      return;
    }
    _completed = true;
    FlushRowGroup();
    _writer.Dispose(); // footer/metadata
    _stream.Dispose();
  }

  public void Dispose() => Complete();

  private void FlushRowGroup()
  {
    if (_buffered == 0)
    {
      return;
    }
    using var rowGroup = _writer.CreateRowGroup();
    for (var i = 0; i < _fields.Length; i++)
    {
      rowGroup.WriteColumnAsync(new DataColumn(_fields[i], _cols[i].ToArrayAndClear())).GetAwaiter().GetResult();
    }
    _buffered = 0;
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
