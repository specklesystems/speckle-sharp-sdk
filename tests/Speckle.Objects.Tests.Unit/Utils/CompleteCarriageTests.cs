using AwesomeAssertions;
using Speckle.Objects.Data;
using Speckle.Objects.Geometry;
using Speckle.Objects.Utils;
using Speckle.Sdk;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;
using Speckle.Sdk.Models.Instances;
using Speckle.Sdk.Pipelines.Receive.Artifacts;

namespace Speckle.Objects.Tests.Unit.Utils;

/// <summary>
/// ENG-9301, the complete-data-carriage floor (the SDK default of <see cref="ArtifactReceiveOptions"/>): objects
/// without their own DISPLAY geometry — definition members, nested-placement carriers, pure property carriers —
/// surface in the tree with their data, joined through the (definition, member-ordinal) vocabulary; decode
/// failures are loud. The connector-bake profile (<c>CompleteCarriage: false</c>) keeps the v1 shape.
/// </summary>
public class CompleteCarriageTests
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

  private static double[] Identity() => [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];

  private static IEnumerable<Base> Flatten(Base b)
  {
    yield return b;
    if (b is Collection c)
    {
      foreach (var child in c.elements.SelectMany(Flatten))
      {
        yield return child;
      }
    }
  }

  private static async Task<T> WithBundle<T>(Action<ObjectsArtifactPipeline> populate, Func<ArtefactBundle, T> use)
  {
    var dir = Path.Combine(Path.GetTempPath(), "SpeckleCompleteCarriage", Guid.NewGuid().ToString("N"));
    try
    {
      using (var pipeline = new ObjectsArtifactPipeline(dir, "cc", TestProducer))
      {
        populate(pipeline);
        pipeline.Complete();
      }
      return use(await ArtefactBundleReader.ReadAsync(dir, default));
    }
    finally
    {
      if (Directory.Exists(dir))
      {
        Directory.Delete(dir, true);
      }
    }
  }

  /// <summary>A fully-instanced graph: a definition with a geometry member (DEFINES_MEMBER join) and a nested
  /// placement member (PLACES), one scene placement. The shape file imports (IFC/Nwd) and Revit families produce —
  /// where the old projection wiped the whole data layer.</summary>
  private static void FullyInstancedGraph(ObjectsArtifactPipeline pipeline)
  {
    int layerK = pipeline.AddCollection("layer-a", "Layer A", null, "Layer");
    int defK = pipeline.AddDefinition("def-chair", "Chair");

    // geometry member: properties + IN_COLLECTION + geometry via the member-ordinal join (no DISPLAY of its own)
    int seatK = pipeline.InternObject("seat-1");
    pipeline.AddProperties("seat-1", new Dictionary<string, object?> { ["Mark"] = "SEAT-01" });
    pipeline.InCollection(seatK, layerK, 0);
    int gK = pipeline.AddGeometry("seat-1:g0", Triangle());
    pipeline.Defines(defK, gK, 0);
    pipeline.DefinesMember(defK, seatK, 0);

    // nested placement member: PLACES + IN_COLLECTION, no geometry of its own
    int cushionDefK = pipeline.AddDefinition("def-cushion", "Cushion");
    int nestedInstK = pipeline.AddInstance("chair/cushion-0", cushionDefK, Identity(), "m");
    pipeline.DefinesInstance(defK, nestedInstK, 1);
    int cushionMemberK = pipeline.InternObject("cushion-1");
    pipeline.AddProperties("cushion-1", new Dictionary<string, object?> { ["Mark"] = "CUSH-01" });
    pipeline.InCollection(cushionMemberK, layerK, 0);
    pipeline.Places(cushionMemberK, nestedInstK);
    pipeline.DefinesMember(defK, cushionMemberK, 1);

    // the scene placement
    int placementK = pipeline.InternObject("chair-placed-1");
    pipeline.AddProperties("chair-placed-1", new Dictionary<string, object?>());
    pipeline.DisplayInstance(placementK, pipeline.AddInstance("chair-placed-1", defK, Identity(), "m"), 0);
    pipeline.InCollection(placementK, layerK, 0);
  }

  [Fact]
  public async Task Members_SurfaceWithDataAndGeometry_ProxiesReferenceThem()
  {
    var root = await WithBundle(
      FullyInstancedGraph,
      bundle => (Collection)new ObjectsArtifactReader().Build(bundle, new(PreferSolids: false), default)
    );

    var all = Flatten(root).ToList();

    // the geometry member is a real object: its properties and its DEFINES geometry, in its own layer
    var seat = all.OfType<DataObject>().Single(b => b.applicationId == "seat-1");
    ((Dictionary<string, object?>)seat.properties["properties"]!)["Mark"].Should().Be("SEAT-01");
    seat.displayValue.Should().HaveCount(1);
    var layer = all.OfType<Collection>().Single(c => c.name == "Layer A");
    layer.elements.Should().Contain(seat);

    // the nested placement surfaces as an InstanceProxy under the member's REAL applicationId, in its layer
    var cushion = all.OfType<InstanceProxy>().Single(p => p.applicationId == "cushion-1");
    layer.elements.Should().Contain(cushion);

    // the definition proxy references the real members — no synthetic def-geo/nested-inst entries anywhere
    var proxies = (List<object>)root["instanceDefinitionProxies"]!;
    var chairDef = proxies.OfType<InstanceDefinitionProxy>().Single(p => p.name == "Chair");
    chairDef.objects.Should().Contain("seat-1");
    chairDef.objects.Should().Contain("cushion-1");
    all.Where(b => b.applicationId?.StartsWith("def-geo-", StringComparison.Ordinal) == true).Should().BeEmpty();
    all.Where(b => b.applicationId?.StartsWith("nested-inst-", StringComparison.Ordinal) == true).Should().BeEmpty();
  }

  [Fact]
  public async Task ConnectorProfile_KeepsTheV1Shape()
  {
    var root = await WithBundle(
      FullyInstancedGraph,
      bundle =>
        (Collection)
          new ObjectsArtifactReader().Build(bundle, new(PreferSolids: false, CompleteCarriage: false), default)
    );

    var all = Flatten(root).ToList();
    all.Where(b => b.applicationId == "seat-1").Should().BeEmpty(); // members dropped, as v1 expects
    all.Where(b => b.applicationId == "cushion-1").Should().BeEmpty();
    // the definition still works through synthesized wrappers
    all.Where(b => b.applicationId?.StartsWith("def-geo-", StringComparison.Ordinal) == true).Should().HaveCount(1);
    all.OfType<InstanceProxy>()
      .Where(p => p.applicationId?.StartsWith("nested-inst-", StringComparison.Ordinal) == true)
      .Should()
      .HaveCount(1);
  }

  [Fact]
  public async Task PurePropertyCarrier_SurfacesByDefault_SkippedOnConnectorProfile()
  {
    static void Populate(ObjectsArtifactPipeline pipeline)
    {
      int k = pipeline.InternObject("level-1");
      pipeline.AddProperties("level-1", new Dictionary<string, object?> { ["Elevation"] = 3.2 });
    }

    var complete = await WithBundle(
      Populate,
      bundle => (Collection)new ObjectsArtifactReader().Build(bundle, new(PreferSolids: false), default)
    );
    var carrier = Flatten(complete).OfType<DataObject>().Single(b => b.applicationId == "level-1");
    carrier.displayValue.Should().BeEmpty();
    ((Dictionary<string, object?>)carrier.properties["properties"]!)["Elevation"].Should().Be(3.2);

    var legacy = await WithBundle(
      Populate,
      bundle =>
        (Collection)
          new ObjectsArtifactReader().Build(bundle, new(PreferSolids: false, CompleteCarriage: false), default)
    );
    Flatten(legacy).Where(b => b.applicationId == "level-1").Should().BeEmpty();
  }

  [Fact]
  public async Task DecodeFailure_IsLoudByDefault_SilentOnConnectorProfile()
  {
    static void Populate(ObjectsArtifactPipeline pipeline)
    {
      int k = pipeline.InternObject("broken-1");
      pipeline.AddProperties("broken-1", new Dictionary<string, object?>());
      // SGEO magic + garbage: IsSgeo says yes, the decoder throws
      int gK = pipeline.AddRawGeometry("broken-1:g0", [0x53, 0x47, 0x45, 0x4F, 9, 9, 9, 9], "sgeo");
      pipeline.Display(k, gK, 0);
    }

    await Assert.ThrowsAsync<SpeckleException>(() =>
      WithBundle(
        Populate,
        bundle => (Collection)new ObjectsArtifactReader().Build(bundle, new(PreferSolids: false), default)
      )
    );

    var legacy = await WithBundle(
      Populate,
      bundle =>
        (Collection)
          new ObjectsArtifactReader().Build(bundle, new(PreferSolids: false, CompleteCarriage: false), default)
    );
    Flatten(legacy).Where(b => b.applicationId == "broken-1").Should().BeEmpty(); // silently dropped, v1 behaviour
  }
}
