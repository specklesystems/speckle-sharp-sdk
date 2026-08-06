using Parquet;
using Parquet.Data;
using Parquet.Schema;
using SpecArrow = Speckle.Bundle.Spec.ArrowType;
using SpecColumn = Speckle.Bundle.Spec.ColumnSpec;

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
#pragma warning disable IDE0370 // the '!' IS required (CS8625 without it): assignment happens on the scheduler thread, invisible to flow analysis
  // Created + owned on the scheduler thread (ctor's enqueued job); read only there — see the ctor for why.
  private Stream _stream = null!;
  private ParquetWriter _writer = null!;
#pragma warning restore IDE0370
#pragma warning restore CA2213
  private readonly DataField[] _fields;
  private readonly Col[] _cols;
  private readonly int _flushRows;
  private readonly ParquetWriteScheduler _scheduler;
  private int _buffered;
  private bool _completed;

  /// <summary>Creates a writer whose shape comes straight from the generated bundle-spec column descriptors
  /// (e.g. <c>BundleSchemas.Nodes</c>) — the single source of truth for spec'd tables. Pass the matching
  /// generated <c>BundleCols.*.ColumnCount</c> as <paramref name="specColumnCount"/>: both files are emitted
  /// by the same spec codegen run, so a mismatch means a stale or partially-synced spec checkout and throws
  /// here, at construction, before a single row can be written misaligned. NOTE this is also the only ctor
  /// usable from OUTSIDE this assembly — the ILRepack internalizes Parquet.Net, so the
  /// <see cref="ParquetSchema"/> overload's parameter type is renamed in the shipped assembly.</summary>
  public ParquetTableWriter(
    string path,
    SpecColumn[] spec,
    int specColumnCount,
    ParquetWriteScheduler scheduler,
    int flushRows = DEFAULT_ROWGROUP_ROWS
  )
    : this(path, SchemaOf(spec, specColumnCount, path), scheduler, flushRows) { }

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
    _fields = schema.GetDataFields();

    _cols = new Col[_fields.Length];
    for (var i = 0; i < _fields.Length; i++)
    {
      _cols[i] = MakeCol(_fields[i]);
    }
    _flushRows = flushRows;

    // Create the stream + writer on the SCHEDULER, never inline: CreateAsync awaits disk IO, and
    // blocking it with .GetResult() on a thread carrying a single-threaded context (the Revit UI
    // thread's DispatcherSynchronizationContext, or the ODA pinned scheduler) deadlocks — the
    // continuation is posted back to the very thread parked in GetResult(). FIFO ordering puts this
    // create ahead of every flush/finalize job, so _stream/_writer live only on the scheduler thread.
    _scheduler.Enqueue(() =>
    {
      _stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
      _writer = ParquetWriter.CreateAsync(schema, _stream).GetAwaiter().GetResult();
      _writer.CompressionMethod = CompressionMethod.Zstd;
    });
  }

  /// <summary>Appends one row; <paramref name="values"/> are in schema-column order. The value count must
  /// match the schema exactly — a mismatch means the schema (typically spec-generated) and this call site
  /// have drifted (e.g. a spec bump inserted a column the caller doesn't supply yet), which would otherwise
  /// silently misalign every row, so it throws instead.</summary>
  public void AddRow(params object?[] values)
  {
    if (_completed)
    {
      throw new InvalidOperationException("Writer already completed.");
    }
    if (values.Length != _cols.Length)
    {
      throw new ArgumentException(
        $"Row arity mismatch for '{Path}': got {values.Length} value(s) for a {_cols.Length}-column schema "
          + $"({string.Join(", ", _fields.Select(f => f.Name))}). The schema and this AddRow call site have "
          + "drifted — update the caller to supply exactly one value per column, in schema order.",
        nameof(values)
      );
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
    // so it never blocks the producer and never races the row-group writes. _writer/_stream are
    // read here inside the job (they're assigned on the scheduler thread by the ctor's create job).
    _scheduler.Enqueue(() =>
    {
      _writer.Dispose(); // footer/metadata
      _stream.Dispose();
    });
  }

  public void Dispose() => Complete();

  // Snapshot the buffered columns into plain arrays so the producer can buffer the next row group
  // immediately, then hand the encode/compress/IO to the scheduler (which alone touches _writer/_stream).
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

    var fields = _fields;
    _scheduler.Enqueue(() =>
    {
      // _writer is read here on the scheduler thread (set by the ctor's create job, which FIFO-precedes this).
      using var rowGroup = _writer.CreateRowGroup();
      for (var i = 0; i < fields.Length; i++)
      {
        rowGroup.WriteColumnAsync(new DataColumn(fields[i], arrays[i])).GetAwaiter().GetResult();
      }
    });
  }

  // Build a Parquet.Net schema from the generated spec column descriptors (the single source of truth
  // for table shapes). DDL nullability → DataField<T> (required) vs DataField<T?> (nullable).
  private static ParquetSchema SchemaOf(SpecColumn[] spec, int specColumnCount, string path)
  {
    if (spec.Length != specColumnCount)
    {
      throw new InvalidOperationException(
        $"Generated spec drift for '{path}': BundleSchemas declares {spec.Length} column(s) but the "
          + $"BundleCols ColumnCount passed is {specColumnCount}. The two generated files come from the "
          + "same codegen run — regenerate speckle-bundle-spec (npm run generate) and re-sync the checkout."
      );
    }
    var fields = new Field[spec.Length];
    for (var i = 0; i < spec.Length; i++)
    {
      fields[i] = ToField(spec[i]);
    }
    return new ParquetSchema(fields);
  }

  private static DataField ToField(SpecColumn c) =>
    c.Type switch
    {
      SpecArrow.Int32 => c.Nullable ? new DataField<int?>(c.Name) : new DataField<int>(c.Name),
      SpecArrow.Int64 => c.Nullable ? new DataField<long?>(c.Name) : new DataField<long>(c.Name),
      SpecArrow.Float64 => c.Nullable ? new DataField<double?>(c.Name) : new DataField<double>(c.Name),
      SpecArrow.Boolean => c.Nullable ? new DataField<bool?>(c.Name) : new DataField<bool>(c.Name),
      SpecArrow.Utf8 => new DataField<string>(c.Name),
      SpecArrow.Binary => new DataField<byte[]>(c.Name),
      _ => throw new NotSupportedException($"Unmapped ArrowType {c.Type} for column {c.Name}"),
    };

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
