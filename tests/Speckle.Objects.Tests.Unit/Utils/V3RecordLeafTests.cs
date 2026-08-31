using AwesomeAssertions;
using Speckle.Objects.Data;
using Speckle.Objects.Geometry;
using Speckle.Objects.Utils;
using Speckle.Sdk;
using Speckle.Sdk.Models.Collections;
using Speckle.Sdk.Pipelines.Receive.Artifacts;

namespace Speckle.Objects.Tests.Unit.Utils;

/// <summary>
/// ENG-9300: a v3 script reads parameter and quantity leaves as <c>{name, value, units}</c> records — the flatten
/// collapses those into rows (value + unit/idn columns), so the Base projection rebuilds them, but ONLY in the
/// subtrees that were records in v3 (<c>Parameters.*</c>, <c>Material Quantities.*</c>). Everything else stays
/// scalar, reproducing v3's mixed shape. Receive3/PropertyView is untouched (flat, scalar).
/// </summary>
public class V3RecordLeafTests
{
  private static readonly SpeckleApplication TestProducer = new()
  {
    HostApplication = "Test",
    HostApplicationVersion = "1.2.3",
    Slug = "test-connector",
    SpeckleVersion = "999.1.0-alpha.1",
  };

  private static Mesh Triangle() =>
    new()
    {
      vertices = new List<double> { 0, 0, 0, 1, 0, 0, 1, 1, 0 },
      faces = new List<int> { 3, 0, 1, 2 },
      units = "m",
    };

  private static Dictionary<string, object?> Field(string name, object? value, string? units = null, string? idn = null)
  {
    var field = new Dictionary<string, object?> { ["name"] = name, ["value"] = value };
    if (units is not null)
    {
      field["units"] = units;
    }
    if (idn is not null)
    {
      field["internalDefinitionName"] = idn;
    }
    return field;
  }

  [Fact]
  public async Task ParameterLeaves_ComeBackAsV3Records_ScalarsStayScalar()
  {
    var dir = Path.Combine(Path.GetTempPath(), "SpeckleV3Records", Guid.NewGuid().ToString("N"));
    try
    {
      using (var pipeline = new ObjectsArtifactPipeline(dir, "v3r", TestProducer))
      {
        int objK = pipeline.InternObject("wall-1");
        pipeline.AddProperties(
          "wall-1",
          new Dictionary<string, object?>
          {
            ["Parameters"] = new Dictionary<string, object?>
            {
              ["Constraints"] = new Dictionary<string, object?>
              {
                ["Base Offset"] = Field("Base Offset", 0.5, "mm", "WALL_BASE_OFFSET"),
              },
              ["Identity Data"] = new Dictionary<string, object?> { ["Mark"] = Field("Mark", "W-01") },
            },
            ["myUserNote"] = "check this wall",
          }
        );
        int gK = pipeline.AddGeometry("wall-1:g0", Triangle());
        pipeline.Display(objK, gK, 0);
        pipeline.Complete();
      }

      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);
      var root = (Collection)new ObjectsArtifactReader().Build(bundle, new(PreferSolids: false), default);
      var wall = root.elements.OfType<DataObject>().Single(b => b.applicationId == "wall-1");
      var props = (Dictionary<string, object?>)wall.properties["properties"]!;
      var parameters = (Dictionary<string, object?>)props["Parameters"]!;

      // the exact v3 access pattern works again: ["value"], ["units"], ["name"], ["internalDefinitionName"]
      var baseOffset =
        (Dictionary<string, object?>)((Dictionary<string, object?>)parameters["Constraints"]!)["Base Offset"]!;
      baseOffset["name"].Should().Be("Base Offset");
      baseOffset["value"].Should().Be(0.5);
      baseOffset["units"].Should().Be("mm");
      baseOffset["internalDefinitionName"].Should().Be("WALL_BASE_OFFSET");

      // a unit-less parameter is a record without a units key — v3's shape for text params
      var mark = (Dictionary<string, object?>)((Dictionary<string, object?>)parameters["Identity Data"]!)["Mark"]!;
      mark["value"].Should().Be("W-01");
      mark.Should().NotContainKey("units");

      // outside the record subtrees, v3 was scalar and stays scalar
      props["myUserNote"].Should().Be("check this wall");
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
