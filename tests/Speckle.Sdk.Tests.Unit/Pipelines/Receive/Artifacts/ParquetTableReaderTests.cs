using AwesomeAssertions;
using Speckle.Bundle.Spec;
using Speckle.Sdk.Pipelines.Receive.Artifacts;
using Speckle.Sdk.Pipelines.Send.Artifacts;

namespace Speckle.Sdk.Tests.Unit.Pipelines.Receive.Artifacts;

public sealed class ParquetTableReaderTests : IDisposable
{
  private readonly string _dir = Path.Combine(Path.GetTempPath(), $"parquet-table-reader-{Guid.NewGuid():N}");

  public ParquetTableReaderTests()
  {
    Directory.CreateDirectory(_dir);
  }

  public void Dispose()
  {
    if (Directory.Exists(_dir))
    {
      Directory.Delete(_dir, recursive: true);
    }
  }

  /// <summary>
  /// Real bundles are written in several row groups (the writer flushes every N rows). Optional columns come back
  /// from Parquet.Net as <c>T?[]</c> per group; concatenating them into a <c>T[]</c> sized off the field's
  /// non-nullable CLR type threw <see cref="ArrayTypeMismatchException"/> — every multi-row-group bundle failed to
  /// read. Single-group fixtures never hit it because one chunk is returned as-is.
  /// </summary>
  [Fact]
  public async Task ReadAsync_MultipleRowGroups_NullableColumnsConcatenate()
  {
    var path = Path.Combine(_dir, "nodes.parquet");
    const int rowsPerGroup = 3;
    const int rows = 7; // → 3 row groups: 3 + 3 + 1
    using (var scheduler = new ParquetWriteScheduler())
    using (
      var writer = new ParquetTableWriter(
        path,
        BundleSchemas.Nodes,
        BundleCols.Nodes.ColumnCount,
        scheduler,
        flushRows: rowsPerGroup
      )
    )
    {
      for (int i = 0; i < rows; i++)
      {
        var row = new object?[BundleCols.Nodes.ColumnCount];
        row[BundleCols.Nodes.Id] = i;
        row[BundleCols.Nodes.Kind] = 1;
        row[BundleCols.Nodes.Name] = i % 2 == 0 ? $"node-{i}" : null;
        row[BundleCols.Nodes.Argb] = i % 3 == 0 ? unchecked((int)0xFF00FF00) : null;
        row[BundleCols.Nodes.Elevation] = i * 1.5;
        writer.AddRow(row);
      }
    }

    var table = await ParquetTableReader.ReadAsync(path);

    table.RowCount.Should().Be(rows);
    table.Ints("id").Should().Equal(Enumerable.Range(0, rows));
    table.Strings("name").Should().Equal(Enumerable.Range(0, rows).Select(i => i % 2 == 0 ? $"node-{i}" : null));
    table
      .NullableInts("argb")
      .Should()
      .Equal(Enumerable.Range(0, rows).Select(i => i % 3 == 0 ? unchecked((int)0xFF00FF00) : (int?)null));
    table.NullableDoubles("elevation").Should().Equal(Enumerable.Range(0, rows).Select(i => (double?)(i * 1.5)));
  }
}
