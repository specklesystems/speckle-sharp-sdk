using AwesomeAssertions;
using Speckle.Bundle.Spec;
using Speckle.Sdk.Pipelines.Receive.Artifacts;
using Speckle.Sdk.Pipelines.Send.Artifacts;

namespace Speckle.Sdk.Tests.Unit.Pipelines.Receive.Artifacts;

/// <summary>
/// Guards the multi-row-group concat in <see cref="ParquetTableReader"/>. Parquet.Net's <c>DataField.ClrType</c>
/// is the non-nullable type for optional columns while the column data comes back as <c>T?[]</c>; building the
/// merged array from <c>ClrType</c> made <c>Array.Copy</c> throw
/// <see cref="ArrayTypeMismatchException"/> ("Source array type cannot be assigned to destination array type")
/// on every table large enough to have been flushed into more than one row group — i.e. only on big models.
/// </summary>
public class ParquetTableReaderTests : IDisposable
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

  private static object?[] NodeRow(int id, int? argb, double? ior)
  {
    var row = new object?[BundleCols.Nodes.ColumnCount];
    row[BundleCols.Nodes.Id] = id;
    row[BundleCols.Nodes.Kind] = 1;
    row[BundleCols.Nodes.Argb] = argb;
    row[BundleCols.Nodes.Ior] = ior;
    return row;
  }

  [Fact]
  public async Task ReadAsync_MultipleRowGroups_ConcatenatesNullableValueTypeColumns()
  {
    var path = Path.Combine(_dir, "nodes.parquet");
    using (var scheduler = new ParquetWriteScheduler())
    {
      // flushRows: 2 → 5 rows land in 3 row groups, forcing the concat path.
      using (
        var writer = new ParquetTableWriter(
          path,
          BundleSchemas.Nodes,
          BundleCols.Nodes.ColumnCount,
          scheduler,
          flushRows: 2
        )
      )
      {
        writer.AddRow(NodeRow(0, 0xFF0000, 1.5));
        writer.AddRow(NodeRow(1, null, null));
        writer.AddRow(NodeRow(2, 0x00FF00, null));
        writer.AddRow(NodeRow(3, null, 2.25));
        writer.AddRow(NodeRow(4, 0x0000FF, 3.0));
        writer.Complete();
      }
      scheduler.CompleteAndWait();
    }

    var table = await ParquetTableReader.ReadAsync(path);

    table.RowCount.Should().Be(5);
    table.Ints("id").Should().Equal(0, 1, 2, 3, 4);
    table.NullableInts("argb").Should().Equal(0xFF0000, null, 0x00FF00, null, 0x0000FF);
    table.NullableDoubles("ior").Should().Equal(1.5, null, null, 2.25, 3.0);
  }

  [Fact]
  public async Task ReadAsync_NoRows_ReturnsEmptyTable()
  {
    var path = Path.Combine(_dir, "nodes.parquet");
    using (var scheduler = new ParquetWriteScheduler())
    {
      using (var writer = new ParquetTableWriter(path, BundleSchemas.Nodes, BundleCols.Nodes.ColumnCount, scheduler))
      {
        writer.Complete();
      }
      scheduler.CompleteAndWait();
    }

    var table = await ParquetTableReader.ReadAsync(path);

    table.RowCount.Should().Be(0);
    table.NullableInts("argb").Should().BeEmpty();
    table.Strings("name").Should().BeEmpty();
  }
}
