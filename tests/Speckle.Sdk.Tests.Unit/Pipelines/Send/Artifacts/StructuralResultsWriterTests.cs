using AwesomeAssertions;
using Parquet;
using Speckle.Bundle.Spec;
using Speckle.Sdk.Pipelines.Send.Artifacts;

namespace Speckle.Sdk.Tests.Unit.Pipelines.Send.Artifacts;

/// <summary>
/// Guards <see cref="StructuralResultsWriter"/>'s spec alignment (ADR-0004): the file must carry all
/// <see cref="BundleCols.StructuralResults.ColumnCount"/> generated-spec columns — <c>element_name</c> and
/// <c>position_label</c> included (the group-level pier/spandrel identity axes) — with each identity shape
/// landing its values in the right columns.
/// </summary>
public class StructuralResultsWriterTests : IDisposable
{
  private readonly string _dir = Path.Combine(Path.GetTempPath(), $"structural-results-writer-{Guid.NewGuid():N}");

  public StructuralResultsWriterTests()
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

  [Fact]
  public async Task AddRow_WritesSpecColumns_ForAllIdentityShapes()
  {
    using (var scheduler = new ParquetWriteScheduler())
    {
      using (var writer = new StructuralResultsWriter(_dir, "rt", scheduler))
      {
        // object-level (frame force), group-level (pier force), model-level (base reaction)
        writer.AddRow(7, null, null, "frameForce", "EQx", "M3", null, 1.5, 2, -12.25, null);
        writer.AddRow(null, "PIER1", "Story4", "pierForce", "Dead", "P", "Top", null, null, 431.7, null);
        writer.AddRow(null, null, null, "baseReaction", "Dead", "FX", null, null, 1, 900.0, null);
        writer.Complete();
      }
      scheduler.CompleteAndWait();
    }

    var path = Path.Combine(_dir, "rt.eav.structural_results.parquet");
    await using var fs = File.OpenRead(path);
    using var reader = await ParquetReader.CreateAsync(fs);
    var fields = reader.Schema.DataFields;
    fields.Should().HaveCount(BundleCols.StructuralResults.ColumnCount);
    fields[BundleCols.StructuralResults.ElementName].Name.Should().Be("element_name");
    fields[BundleCols.StructuralResults.PositionLabel].Name.Should().Be("position_label");

    using var rg = reader.OpenRowGroupReader(0);
    async Task<Array> Col(int ordinal) => (await rg.ReadColumnAsync(fields[ordinal])).Data;

    ((int?[])await Col(BundleCols.StructuralResults.ObjectIndex)).Should().Equal(7, null, null);
    ((string?[])await Col(BundleCols.StructuralResults.ElementName)).Should().Equal(null, "PIER1", null);
    ((string?[])await Col(BundleCols.StructuralResults.Location)).Should().Equal(null, "Story4", null);
    ((string?[])await Col(BundleCols.StructuralResults.ResultType))
      .Should()
      .Equal("frameForce", "pierForce", "baseReaction");
    ((string?[])await Col(BundleCols.StructuralResults.PositionLabel)).Should().Equal(null, "Top", null);
    ((double?[])await Col(BundleCols.StructuralResults.Station)).Should().Equal(1.5, null, null);
    ((int?[])await Col(BundleCols.StructuralResults.Step)).Should().Equal(2, null, 1);
    ((double?[])await Col(BundleCols.StructuralResults.Value)).Should().Equal(-12.25, 431.7, 900.0);
    ((string?[])await Col(BundleCols.StructuralResults.ValueText)).Should().Equal(null, null, null);
  }
}
