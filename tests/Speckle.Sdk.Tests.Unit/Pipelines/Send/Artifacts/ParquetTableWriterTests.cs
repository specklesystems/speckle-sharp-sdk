using AwesomeAssertions;
using Parquet;
using Speckle.Bundle.Spec;
using Speckle.Sdk.Pipelines.Send.Artifacts;

namespace Speckle.Sdk.Tests.Unit.Pipelines.Send.Artifacts;

/// <summary>
/// Guards the <see cref="ParquetTableWriter.AddRow"/> arity assert on a spec-schema'd table: a row whose
/// value count doesn't match the schema must throw (naming the file and both counts) instead of silently
/// misaligning — the failure mode behind the Jul 29 empty-nodes incident, where a spec column insertion
/// (emissive/ior, 12 → 14) outran positional call sites and every row was dropped. Uses the generated
/// <c>BundleSchemas.Nodes</c> + <c>BundleCols.Nodes.ColumnCount</c> pair, i.e. the exact construction path
/// <see cref="EnvelopeWriter"/> uses (the repack internalizes Parquet.Net, so the spec-based ctor is also
/// the only one visible out here).
/// </summary>
public class ParquetTableWriterTests : IDisposable
{
  private readonly string _dir = Path.Combine(Path.GetTempPath(), $"parquet-table-writer-{Guid.NewGuid():N}");

  public ParquetTableWriterTests()
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

  // A full 14-value nodes row in BundleCols order — assembled by named ordinal, the adoption pattern
  // the constants exist for (an inserted spec column re-numbers these at the next regen).
  private static object?[] NodeRow(int id, string? name = null)
  {
    var row = new object?[BundleCols.Nodes.ColumnCount];
    row[BundleCols.Nodes.Id] = id;
    row[BundleCols.Nodes.Kind] = 1;
    row[BundleCols.Nodes.Name] = name;
    return row;
  }

  [Fact]
  public void AddRow_TooFewValues_ThrowsNamingFileAndCounts()
  {
    var path = Path.Combine(_dir, "nodes.parquet");
    using var scheduler = new ParquetWriteScheduler();
    using var writer = new ParquetTableWriter(path, BundleSchemas.Nodes, BundleCols.Nodes.ColumnCount, scheduler);

    // The incident shape: a pre-emissive/ior call site still supplying 12 values to the 14-column schema.
    var act = () => writer.AddRow(0, 1, "wall", null, null, "mm", null, null, null, null, null, null);

    act.Should()
      .Throw<ArgumentException>()
      .WithMessage($"*{path}*")
      .WithMessage("*12 value(s)*")
      .WithMessage($"*{BundleCols.Nodes.ColumnCount}-column*")
      .WithMessage("*emissive, ior*");
  }

  [Fact]
  public void AddRow_TooManyValues_Throws()
  {
    var path = Path.Combine(_dir, "nodes.parquet");
    using var scheduler = new ParquetWriteScheduler();
    using var writer = new ParquetTableWriter(path, BundleSchemas.Nodes, BundleCols.Nodes.ColumnCount, scheduler);

    var row = NodeRow(0);
    var act = () => writer.AddRow([.. row, "surplus"]);

    act.Should()
      .Throw<ArgumentException>()
      .WithMessage($"*{BundleCols.Nodes.ColumnCount + 1} value(s)*")
      .WithMessage($"*{BundleCols.Nodes.ColumnCount}-column*");
  }

  [Fact]
  public async Task AddRow_ExactArity_RoundTrips()
  {
    var path = Path.Combine(_dir, "nodes.parquet");
    using (var scheduler = new ParquetWriteScheduler())
    {
      using (var writer = new ParquetTableWriter(path, BundleSchemas.Nodes, BundleCols.Nodes.ColumnCount, scheduler))
      {
        writer.AddRow(NodeRow(0, "wall-def"));
        writer.AddRow(NodeRow(1));
        writer.Complete();
      }
      scheduler.CompleteAndWait();
    }

    await using var fs = File.OpenRead(path);
    using var reader = await ParquetReader.CreateAsync(fs);
    reader.Schema.DataFields.Should().HaveCount(BundleCols.Nodes.ColumnCount);
    reader.Schema.DataFields[BundleCols.Nodes.Emissive].Name.Should().Be("emissive");
    using var rg = reader.OpenRowGroupReader(0);
    var ids = await rg.ReadColumnAsync(reader.Schema.DataFields[BundleCols.Nodes.Id]);
    ((int[])ids.Data).Should().Equal(0, 1);
    var names = await rg.ReadColumnAsync(reader.Schema.DataFields[BundleCols.Nodes.Name]);
    ((string?[])names.Data).Should().Equal("wall-def", null);
  }
}
