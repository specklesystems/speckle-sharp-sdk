using AwesomeAssertions;
using Parquet;
using Speckle.Bundle.Spec;
using Speckle.Objects.Geometry;
using Speckle.Objects.Utils;
using Speckle.Sdk.BundleMigrator.Migration;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;
using Speckle.Sdk.Pipelines.Receive.Artifacts;

namespace Speckle.Sdk.BundleMigrator.Tests;

/// <summary>
/// Guards ENG-9076: a v3 CSi root's <c>analysisResults</c> flattens into <c>structural_results</c> rows
/// (all 8 result types, big-truck's element-name→appId resolution with its location fallback) and the unit
/// scalars land as <c>units.*</c> rows in <c>eav.model</c> — while a root without them produces neither file.
/// </summary>
public class V3GraphArtifactProducerStructuralResultsTests
{
  public V3GraphArtifactProducerStructuralResultsTests()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(Mesh).Assembly);
  }

  private static readonly SpeckleApplication TestProducer = new()
  {
    HostApplication = "Test",
    HostApplicationVersion = "1.2.3",
    Slug = "test-connector",
    SpeckleVersion = "999.1.0-alpha.1",
  };

  private static Mesh NamedMesh(string appId, string name)
  {
    var mesh = new Mesh
    {
      vertices = new List<double> { 0, 0, 0, 1, 0, 0, 1, 1, 0 },
      faces = new List<int> { 3, 0, 1, 2 },
      units = "m",
      applicationId = appId,
      id = appId,
    };
    mesh["name"] = name;
    return mesh;
  }

  private static Stats Migrate(Collection root, string dir)
  {
    using var producer = new V3GraphArtifactProducer(
      new ObjectsArtifactPipeline(dir, "v3", TestProducer),
      new ArtifactHelper()
    );
    return producer.Produce(root);
  }

  private static Dictionary<string, object?> D(params (string Key, object? Value)[] entries)
  {
    var d = new Dictionary<string, object?>();
    foreach (var (key, value) in entries)
    {
      d[key] = value;
    }
    return d;
  }

  private sealed record Row(
    int? ObjectIndex,
    string? ElementName,
    string? Location,
    string ResultType,
    string LoadCase,
    string Component,
    string? PositionLabel,
    double? Station,
    int? Step,
    double? Value
  );

  private static async Task<List<Row>> ReadStructuralRows(string dir)
  {
    await using var fs = File.OpenRead(Path.Combine(dir, "v3.eav.structural_results.parquet"));
    using var reader = await ParquetReader.CreateAsync(fs);
    var fields = reader.Schema.DataFields;
    fields.Should().HaveCount(BundleCols.StructuralResults.ColumnCount);
    using var rg = reader.OpenRowGroupReader(0);
    async Task<Array> Col(int ordinal) => (await rg.ReadColumnAsync(fields[ordinal])).Data;

    var objectIndex = (int?[])await Col(BundleCols.StructuralResults.ObjectIndex);
    var elementName = (string?[])await Col(BundleCols.StructuralResults.ElementName);
    var location = (string?[])await Col(BundleCols.StructuralResults.Location);
    var resultType = (string[])await Col(BundleCols.StructuralResults.ResultType);
    var loadCase = (string[])await Col(BundleCols.StructuralResults.LoadCase);
    var component = (string[])await Col(BundleCols.StructuralResults.Component);
    var positionLabel = (string?[])await Col(BundleCols.StructuralResults.PositionLabel);
    var station = (double?[])await Col(BundleCols.StructuralResults.Station);
    var step = (int?[])await Col(BundleCols.StructuralResults.Step);
    var value = (double?[])await Col(BundleCols.StructuralResults.Value);

    var rows = new List<Row>();
    for (var i = 0; i < resultType.Length; i++)
    {
      rows.Add(
        new Row(
          objectIndex[i],
          elementName[i],
          location[i],
          resultType[i],
          loadCase[i],
          component[i],
          positionLabel[i],
          station[i],
          step[i],
          value[i]
        )
      );
    }
    return rows;
  }

  private static async Task<int> ObjectIndexOf(string dir, string applicationId)
  {
    await using var fs = File.OpenRead(Path.Combine(dir, "v3.eav.objects.parquet"));
    using var reader = await ParquetReader.CreateAsync(fs);
    var fields = reader.Schema.DataFields;
    using var rg = reader.OpenRowGroupReader(0);
    var ks = (int[])(await rg.ReadColumnAsync(fields[BundleCols.Objects.ObjectIndex])).Data;
    var appIds = (string[])(await rg.ReadColumnAsync(fields[BundleCols.Objects.ApplicationId])).Data;
    return ks[Array.IndexOf(appIds, applicationId)];
  }

  [Fact]
  public async Task AnalysisResults_FlattenAcrossAllIdentityShapes_AndUnitsRideEavModel()
  {
    var dir = Path.Combine(Path.GetTempPath(), "SpeckleV3Structural", Guid.NewGuid().ToString("N"));
    try
    {
      var root = new Collection
      {
        name = "root",
        applicationId = "root",
        elements = { NamedMesh("frame1", "F1"), NamedMesh("other1", "X9") },
      };
      root["units"] = "mm";
      root["forceUnits"] = "kN";
      root["temperatureUnits"] = "NotApplicable"; // silent GetDatabaseUnits_2 failure — must be skipped
      root["materialProxies"] = new List<object> { new Base() };

      var results = new Base();
      // The extractor shape: grouping keys as dict levels, Wrap: keys adding a literal-named level.
      static Dictionary<string, object?> FrameCase(string sta, string stepNum, Dictionary<string, object?> leaf) =>
        D(("EQx", D(("ElmSta", D((sta, D(("StepNum", D((stepNum, leaf))))))))));

      // F1 resolves to the sent mesh; F2 is an analysis-only element name → location fallback.
      results["frameForces"] = D(
        ("F1", FrameCase("1.5", "2", D(("P", 10.5), ("M3", -12.25)))),
        ("F2", FrameCase("0", "1", D(("P", 3.25))))
      );
      // Long leaf (integral JSON) plus the stray GX/GY/GZ centroid doubles beside the load-case keys.
      results["baseReact"] = D(
        ("Dead", D(("StepNum", D(("1", D(("FX", 1.1), ("ParamMy", 4L))))))),
        ("GX", 5.0),
        ("GY", 6.0),
        ("GZ", 7.0)
      );
      results["pierForces"] = D(("PIER1", D(("Story1", D(("Dead", D(("Location", D(("Top", D(("P", 431.7))))))))))));
      results["storyDrifts"] = D(
        (
          "Story1",
          D(
            (
              "EQx",
              D(
                (
                  "StepNum",
                  D(("1", D(("Direction", "X"), ("Drift", 0.0021), ("Label", "5"), ("X", 1.0), ("Y", 2.0), ("Z", 3.0))))
                )
              )
            )
          )
        )
      );
      results["storyForces"] = D(("Story1", D(("Dead", D(("Top", D(("P", 100.0), ("VX", 50.0))))))));
      results["memberForces"] = D(("M1", D(("Dead", D(("ratio", 0.87)))))); // TSD-shaped: no mapping → counted
      root["analysisResults"] = results;

      var stats = Migrate(root, dir);
      var rows = await ReadStructuralRows(dir);

      // frameForce: F1 joins back to the sent mesh's K; F2 keeps its raw name in `location`.
      var frameK = await ObjectIndexOf(dir, "frame1");
      var frame = rows.Where(r => r.ResultType == "frameForce").ToList();
      frame.Should().HaveCount(3);
      var f1 = frame.Where(r => r.ObjectIndex == frameK).ToList();
      f1.Select(r => (r.Component, r.Value)).Should().BeEquivalentTo([("P", 10.5), ("M3", -12.25)]);
      f1.Should().OnlyContain(r => r.LoadCase == "EQx" && r.Station == 1.5 && r.Step == 2 && r.Location == null);
      var f2 = frame.Single(r => r.ObjectIndex == null);
      (f2.Location, f2.Component, f2.Value, f2.Station).Should().Be(("F2", "P", 3.25, 0.0));

      // baseReaction: model-level, verbatim components (incl. ParamMy), centroid scalars never became rows.
      var baseR = rows.Where(r => r.ResultType == "baseReaction").ToList();
      baseR.Select(r => (r.Component, r.Value)).Should().BeEquivalentTo([("FX", 1.1), ("ParamMy", 4.0)]);
      baseR
        .Should()
        .OnlyContain(r =>
          r.ObjectIndex == null && r.ElementName == null && r.Location == null && r.LoadCase == "Dead" && r.Step == 1
        );

      // pierForce: group-level identity — element_name + story + Top/Bottom, no object K.
      var pier = rows.Single(r => r.ResultType == "pierForce");
      (pier.ObjectIndex, pier.ElementName, pier.Location, pier.PositionLabel, pier.Value)
        .Should()
        .Be((null, "PIER1", "Story1", "Top", 431.7));

      // storyDrift pivot: one row per leaf — Drift per Direction; Label and X/Y/Z dropped (spec locked decision).
      var drift = rows.Single(r => r.ResultType == "storyDrift");
      (drift.Location, drift.Component, drift.Value, drift.PositionLabel, drift.Step)
        .Should()
        .Be(("Story1", "drift", 0.0021, "X", 1));

      // storyForce: DB-table component codes renamed to the spec catalog's semantic names.
      var story = rows.Where(r => r.ResultType == "storyForce").ToList();
      story.Select(r => (r.Component, r.Value)).Should().BeEquivalentTo([("axial", 100.0), ("majorShear", 50.0)]);
      story.Should().OnlyContain(r => r.Location == "Story1" && r.PositionLabel == "Top");

      // Units: distance + force land in eav.model; NotApplicable temperature is skipped.
      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);
      var units = bundle
        .ModelProperties["units"]
        .Should()
        .BeAssignableTo<IReadOnlyDictionary<string, object?>>()
        .Subject;
      units["distance"].Should().Be("mm");
      units["force"].Should().Be("kN");
      units.Should().NotContainKey("temperature");

      stats.StructuralResultRows.Should().Be(9);
      stats.StructuralElmFallbacks.Should().Be(1);
      stats.SkippedResultTypes.Should().Be(1);
      stats.ModelUnitRows.Should().Be(2);
      stats.Notes.Should().Contain(n => n.Contains("memberForces"));
      stats.Notes.Should().Contain(n => n.Contains("materialProxies"));
    }
    finally
    {
      if (Directory.Exists(dir))
      {
        Directory.Delete(dir, true);
      }
    }
  }

  [Fact]
  public void NonCsiRoot_ProducesNeitherOptionalFile()
  {
    var dir = Path.Combine(Path.GetTempPath(), "SpeckleV3Structural", Guid.NewGuid().ToString("N"));
    try
    {
      var root = new Collection
      {
        name = "root",
        applicationId = "root",
        elements = { NamedMesh("m1", "M1") },
      };

      var stats = Migrate(root, dir);

      File.Exists(Path.Combine(dir, "v3.eav.structural_results.parquet")).Should().BeFalse();
      File.Exists(Path.Combine(dir, "v3.eav.model.parquet")).Should().BeFalse();
      stats.StructuralResultRows.Should().Be(0);
      stats.ModelUnitRows.Should().Be(0);
    }
    finally
    {
      if (Directory.Exists(dir))
      {
        Directory.Delete(dir, true);
      }
    }
  }
}
