#if NETSTANDARD2_0 || NET8_0_OR_GREATER
using System.Globalization;
using Parquet;
using Parquet.Data;

namespace Speckle.Sdk.Pipelines.Receive.Artifacts;

/// <summary>
/// Reads one Speckle 4.0 artefact parquet file into a column-oriented <see cref="ParquetTable"/>. The inverse of
/// the <c>Pipelines/Send/Artifacts</c> writers — used by the receive path to rebuild the object graph from a
/// downloaded bundle. Concatenates all row groups; columns are exposed as typed (nullable-aware) accessors.
/// </summary>
public static class ParquetTableReader
{
  public static async Task<ParquetTable> ReadAsync(string path, CancellationToken cancellationToken = default)
  {
    using var stream = File.OpenRead(path);
    using var reader = await ParquetReader.CreateAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

    var fields = reader.Schema.GetDataFields();
    // name -> the per-row-group Data arrays, concatenated at the end.
    var parts = new Dictionary<string, List<Array>>(StringComparer.Ordinal);
    foreach (var f in fields)
    {
      parts[f.Name] = new List<Array>();
    }

    int rowCount = 0;
    for (int g = 0; g < reader.RowGroupCount; g++)
    {
      using var rg = reader.OpenRowGroupReader(g);
      int groupRows = -1;
      foreach (var f in fields)
      {
        DataColumn col = await rg.ReadColumnAsync(f, cancellationToken).ConfigureAwait(false);
        parts[f.Name].Add(col.Data);
        groupRows = col.Data.Length;
      }
      if (groupRows > 0)
      {
        rowCount += groupRows;
      }
    }

    var columns = new Dictionary<string, Array>(StringComparer.Ordinal);
    foreach (var f in fields)
    {
      columns[f.Name] = Concat(parts[f.Name], f.ClrType);
    }
    return new ParquetTable(columns, rowCount);
  }

  // Concatenates the per-row-group column arrays into one array of the column's element type. The element type is
  // the field's CLR type (nullable for optional columns), so Array.Copy preserves null slots.
  private static Array Concat(List<Array> chunks, Type elementType)
  {
    if (chunks.Count == 1)
    {
      return chunks[0];
    }
    int total = 0;
    foreach (var c in chunks)
    {
      total += c.Length;
    }
    var result = Array.CreateInstance(elementType, total);
    int offset = 0;
    foreach (var c in chunks)
    {
      Array.Copy(c, 0, result, offset, c.Length);
      offset += c.Length;
    }
    return result;
  }

  internal static int AsInt(object? v) =>
    v switch
    {
      int i => i,
      long l => (int)l,
      null => 0,
      _ => Convert.ToInt32(v, CultureInfo.InvariantCulture),
    };
}

/// <summary>One artefact parquet table, column-oriented. Accessors are nullable-aware (parquet optional columns
/// come back as <c>T?[]</c>; required ones as <c>T[]</c>).</summary>
public sealed class ParquetTable
{
  private readonly Dictionary<string, Array> _columns;

  public ParquetTable(Dictionary<string, Array> columns, int rowCount)
  {
    _columns = columns;
    RowCount = rowCount;
  }

  public int RowCount { get; }

  public bool Has(string column) => _columns.ContainsKey(column);

  /// <summary>int (or int?) column → int[] (nulls become 0).</summary>
  public int[] Ints(string column)
  {
    var arr = _columns[column];
    var result = new int[arr.Length];
    for (int i = 0; i < arr.Length; i++)
    {
      result[i] = ParquetTableReader.AsInt(arr.GetValue(i));
    }
    return result;
  }

  /// <summary>int? column → int?[] (preserves nulls).</summary>
  public int?[] NullableInts(string column)
  {
    var arr = _columns[column];
    var result = new int?[arr.Length];
    for (int i = 0; i < arr.Length; i++)
    {
      var v = arr.GetValue(i);
      result[i] = v is null ? null : ParquetTableReader.AsInt(v);
    }
    return result;
  }

  public double?[] NullableDoubles(string column)
  {
    var arr = _columns[column];
    var result = new double?[arr.Length];
    for (int i = 0; i < arr.Length; i++)
    {
      result[i] = arr.GetValue(i) as double?;
    }
    return result;
  }

  public bool?[] NullableBools(string column)
  {
    var arr = _columns[column];
    var result = new bool?[arr.Length];
    for (int i = 0; i < arr.Length; i++)
    {
      result[i] = arr.GetValue(i) as bool?;
    }
    return result;
  }

  public string?[] Strings(string column)
  {
    var arr = _columns[column];
    var result = new string?[arr.Length];
    for (int i = 0; i < arr.Length; i++)
    {
      result[i] = arr.GetValue(i) as string;
    }
    return result;
  }

  public byte[]?[] Blobs(string column)
  {
    var arr = _columns[column];
    var result = new byte[]?[arr.Length];
    for (int i = 0; i < arr.Length; i++)
    {
      result[i] = arr.GetValue(i) as byte[];
    }
    return result;
  }
}
#endif
