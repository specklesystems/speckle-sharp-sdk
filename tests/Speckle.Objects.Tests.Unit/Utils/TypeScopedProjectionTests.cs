using AwesomeAssertions;
using Speckle.Objects.Data;
using Speckle.Objects.Geometry;
using Speckle.Objects.Utils;
using Speckle.Sdk;
using Speckle.Sdk.Models.Collections;
using Speckle.Sdk.Pipelines.Receive.Artifacts;

namespace Speckle.Objects.Tests.Unit.Utils;

/// <summary>
/// ENG-9302: the Base projection merges type-scoped params (deduped into <c>type_eav</c>) back onto every element,
/// restoring the v3 shape legacy scripts read (<c>properties.Parameters.Type Parameters.*</c>, the compound
/// <c>Structure</c>) — while keeping the one-parse-per-type sharing.
/// </summary>
public class TypeScopedProjectionTests
{
  private static readonly SpeckleApplication TestProducer = new()
  {
    HostApplication = "Test",
    HostApplicationVersion = "1.2.3",
    Slug = "test-connector",
    SpeckleVersion = "999.1.0-alpha.1",
  };

  private static Mesh UnitTriangle() =>
    new()
    {
      vertices = new List<double> { 0, 0, 0, 1, 0, 0, 1, 1, 0 },
      faces = new List<int> { 3, 0, 1, 2 },
      units = "m",
    };

  private static Dictionary<string, object?> WallProps() =>
    new()
    {
      ["Parameters"] = new Dictionary<string, object?>
      {
        ["Constraints"] = new Dictionary<string, object?> { ["Base Offset"] = 0.5 },
        ["Type Parameters"] = new Dictionary<string, object?>
        {
          ["Construction"] = new Dictionary<string, object?> { ["Width"] = 265.0 },
          ["Structure"] = new Dictionary<string, object?>
          {
            ["0"] = new Dictionary<string, object?> { ["material"] = "White Concrete", ["thickness"] = 65.0 },
          },
        },
      },
    };

  [Fact]
  public async Task TypeParams_MergeBackOntoEveryElement_SharedPerType()
  {
    var dir = Path.Combine(Path.GetTempPath(), "SpeckleTypeProjection", Guid.NewGuid().ToString("N"));
    try
    {
      using (var pipeline = new ObjectsArtifactPipeline(dir, "tp", TestProducer))
      {
        foreach (var id in new[] { "wall-1", "wall-2" })
        {
          int objK = pipeline.InternObject(id);
          pipeline.AddProperties(id, WallProps(), null, typeKey: "type-basic-wall");
          int gK = pipeline.AddGeometry($"{id}:g0", UnitTriangle());
          pipeline.Display(objK, gK, 0);
        }
        pipeline.Complete();
      }

      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);
      var root = (Collection)new ObjectsArtifactReader().Build(bundle, new(false), default);

      var walls = root.elements.OfType<DataObject>().OrderBy(o => o.applicationId).ToList();
      walls.Should().HaveCount(2);

      foreach (var wall in walls)
      {
        var properties = (Dictionary<string, object?>)wall.properties["properties"]!;
        var parameters = (Dictionary<string, object?>)properties["Parameters"]!;
        // instance-scoped stays put
        ((Dictionary<string, object?>)parameters["Constraints"]!)["Base Offset"]
          .Should()
          .Be(0.5);
        // type-scoped merged back — the v3 shape
        var typeParams = (Dictionary<string, object?>)parameters["Type Parameters"]!;
        ((Dictionary<string, object?>)typeParams["Construction"]!)["Width"].Should().Be(265.0);
        var layer0 = (Dictionary<string, object?>)((Dictionary<string, object?>)typeParams["Structure"]!)["0"]!;
        layer0["material"].Should().Be("White Concrete");
        layer0["thickness"].Should().Be(65.0);
      }

      // one-parse-per-type: the type-only subtree is the SAME dictionary instance on both walls (copy-on-write
      // merge shares what has no instance-side counterpart).
      static object? TypeParamsOf(DataObject o) =>
        ((Dictionary<string, object?>)((Dictionary<string, object?>)o.properties["properties"]!)["Parameters"]!)[
          "Type Parameters"
        ];
      TypeParamsOf(walls[0]).Should().BeSameAs(TypeParamsOf(walls[1]));
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
