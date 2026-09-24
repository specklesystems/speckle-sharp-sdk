using AwesomeAssertions;
using Speckle.Objects.Data;
using Speckle.Objects.Geometry;
using Speckle.Objects.Primitive;
using Speckle.Objects.Utils;
using Speckle.Sdk.BundleMigrator.Migration;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;
using Speckle.Sdk.Pipelines.Receive.Artifacts;

namespace Speckle.Sdk.BundleMigrator.Tests;

/// <summary>
/// Guards ENG-9577: legacy Revit versions shipped plane basis vectors scaled by the feet→document-units factor.
/// The SGEO consumer samples curves along that basis, so every plane-bearing curve must migrate with a unit basis.
/// </summary>
public class V3GraphArtifactProducerPlaneNormalizationTests
{
  private const double FEET_TO_MM = 304.8;

  public V3GraphArtifactProducerPlaneNormalizationTests()
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

  private static string TempDir() =>
    Path.Combine(Path.GetTempPath(), "SpeckleV3PlaneNormalization", Guid.NewGuid().ToString("N"));

  private static Plane ScaledPlane() =>
    new()
    {
      origin = new Point(10, 20, 30, "mm"),
      normal = new Vector(0, 0, FEET_TO_MM, "mm"),
      xdir = new Vector(FEET_TO_MM, 0, 0, "mm"),
      ydir = new Vector(0, FEET_TO_MM, 0, "mm"),
      units = "mm",
    };

  private static DataObject Host(string appId, Base display) =>
    new()
    {
      name = appId,
      applicationId = appId,
      id = appId,
      displayValue = new List<Base> { display },
      properties = new Dictionary<string, object?>(),
    };

  private static async Task<Base> MigrateSingleGeometry(Base display)
  {
    var dir = TempDir();
    try
    {
      var root = new Collection { name = "root", applicationId = "root" };
      root.elements.Add(Host("host", display));

      using (
        var producer = new V3GraphArtifactProducer(
          new ObjectsArtifactPipeline(dir, "v3", TestProducer),
          new ArtifactHelper()
        )
      )
      {
        producer.Produce(root);
      }

      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);
      return SgeoDecoder.Decode(bundle.Geometries.Values.Single().Content);
    }
    finally
    {
      if (Directory.Exists(dir))
      {
        Directory.Delete(dir, true);
      }
    }
  }

  private static void AssertUnitBasis(Plane plane)
  {
    plane.xdir.Length.Should().BeApproximately(1, 1e-9);
    plane.ydir.Length.Should().BeApproximately(1, 1e-9);
    plane.normal.Length.Should().BeApproximately(1, 1e-9);
  }

  [Fact]
  public async Task Circle_MigratesWithUnitBasis_AndUnchangedRadius()
  {
    var circle = await MigrateSingleGeometry(
      new Circle
      {
        radius = 500,
        plane = ScaledPlane(),
        units = "mm",
        applicationId = "circle",
        id = "circle",
      }
    );

    var migrated = circle.Should().BeOfType<Circle>().Subject;
    AssertUnitBasis(migrated.plane);
    migrated.radius.Should().Be(500);
  }

  [Fact]
  public async Task Ellipse_MigratesWithUnitBasis()
  {
    var ellipse = await MigrateSingleGeometry(
      new Ellipse
      {
        firstRadius = 500,
        secondRadius = 250,
        plane = ScaledPlane(),
        domain = Interval.UnitInterval,
        units = "mm",
        applicationId = "ellipse",
        id = "ellipse",
      }
    );

    AssertUnitBasis(ellipse.Should().BeOfType<Ellipse>().Subject.plane);
  }

  [Fact]
  public async Task Arc_MigratesWithUnitBasis()
  {
    var arc = await MigrateSingleGeometry(
      new Arc
      {
        plane = ScaledPlane(),
        startPoint = new Point(510, 20, 30, "mm"),
        midPoint = new Point(10, 520, 30, "mm"),
        endPoint = new Point(-490, 20, 30, "mm"),
        domain = Interval.UnitInterval,
        units = "mm",
        applicationId = "arc",
        id = "arc",
      }
    );

    AssertUnitBasis(arc.Should().BeOfType<Arc>().Subject.plane);
  }
}
