using AwesomeAssertions;
using Speckle.Objects.Geometry;
using Speckle.Objects.Utils;
using Speckle.Sdk.BundleMigrator.Migration;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;
using Speckle.Sdk.Pipelines.Receive.Artifacts;

namespace Speckle.Sdk.BundleMigrator.Tests;

/// <summary>
/// Guards ENG-9388: a v3 Grasshopper <see cref="RootCollection"/>'s model-wide <c>properties</c> re-emit as
/// bare dotted-path eav.model rows — the same layout the GH 4.0 Publish writes — so the 4.0 Load nests them back.
/// </summary>
public class V3GraphArtifactProducerModelPropertiesTests
{
  public V3GraphArtifactProducerModelPropertiesTests()
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

  private static Mesh UnitMesh(string appId) =>
    new()
    {
      vertices = new List<double> { 0, 0, 0, 1, 0, 0, 1, 1, 0 },
      faces = new List<int> { 3, 0, 1, 2 },
      units = "m",
      applicationId = appId,
      id = appId,
    };

  private static RootCollection GhRoot(Dictionary<string, object?> properties) =>
    new()
    {
      name = "Grasshopper Model",
      applicationId = "root",
      elements = { UnitMesh("m1") },
      properties = properties,
    };

  private static Stats Migrate(Collection root, string dir)
  {
    using var producer = new V3GraphArtifactProducer(
      new ObjectsArtifactPipeline(dir, "v3", TestProducer),
      new ArtifactHelper()
    );
    return producer.Produce(root);
  }

  private static string TempDir() =>
    Path.Combine(Path.GetTempPath(), "SpeckleV3ModelProps", Guid.NewGuid().ToString("N"));

  private static IReadOnlyDictionary<string, object?> AsGroup(object? value) =>
    value.Should().BeAssignableTo<IReadOnlyDictionary<string, object?>>().Subject;

  [Fact]
  public async Task NestedGroup_EmitsBareDottedPaths_TypedLeaves()
  {
    var dir = TempDir();
    try
    {
      var root = GhRoot(
        new()
        {
          ["Project"] = new Dictionary<string, object?>
          {
            ["Client"] = "ACME",
            ["Storeys"] = 12L, // the deserializer yields integral JSON as long
            ["Area"] = 1250.5,
            ["Approved"] = true,
          },
          ["Author"] = "Jedd",
        }
      );

      var stats = Migrate(root, dir);

      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);
      bundle.ModelProperties.Should().NotContainKey("properties");
      bundle.ModelProperties["Author"].Should().Be("Jedd");
      var project = AsGroup(bundle.ModelProperties["Project"]);
      project["Client"].Should().Be("ACME");
      project["Storeys"].Should().Be(12d);
      project["Area"].Should().Be(1250.5);
      project["Approved"].Should().Be(true);
      stats.ModelPropertyRows.Should().Be(5);
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
  public async Task ListLeaf_JoinedToString()
  {
    var dir = TempDir();
    try
    {
      var root = GhRoot(
        new()
        {
          ["Tags"] = new List<object?>
          {
            "a",
            2L,
            3.5,
            true,
            null,
            new Vector
            {
              x = 1,
              y = 2,
              z = 3,
              units = "m",
            },
          },
          ["Empty"] = new List<object?>(),
        }
      );

      var stats = Migrate(root, dir);

      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);
      bundle.ModelProperties["Tags"].Should().Be("a,2,3.5,true");
      bundle.ModelProperties.Should().NotContainKey("Empty");
      stats.ModelPropertyRows.Should().Be(1);
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
  public async Task BaseLeaf_SkippedWithNote_NullDropped_SiblingsKept()
  {
    var dir = TempDir();
    try
    {
      var root = GhRoot(
        new()
        {
          ["Origin"] = new Plane
          {
            origin = new Point(0, 0, 0, "m"),
            normal = new Vector(0, 0, 1, "m"),
            xdir = new Vector(1, 0, 0, "m"),
            ydir = new Vector(0, 1, 0, "m"),
            units = "m",
          },
          ["Nothing"] = null,
          ["Name"] = "kept",
        }
      );

      var stats = Migrate(root, dir);

      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);
      bundle.ModelProperties.Should().NotContainKey("Origin");
      bundle.ModelProperties.Should().NotContainKey("Nothing");
      bundle.ModelProperties["Name"].Should().Be("kept");
      stats.ModelPropertyRows.Should().Be(1);
      stats.SkippedModelProperties.Should().Be(1);
      stats.Notes.Should().ContainSingle(n => n.Contains("'Origin'") && n.Contains("Plane"));
      stats.Objects.Should().Be(1); // the Plane never becomes an element
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
  public async Task NoProperties_EmitsNoFile()
  {
    var dir = TempDir();
    try
    {
      var stats = Migrate(GhRoot(new()), dir);

      File.Exists(Path.Combine(dir, "v3.eav.model.parquet")).Should().BeFalse();
      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);
      bundle.ModelProperties.Should().BeEmpty();
      stats.ModelPropertyRows.Should().Be(0);
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
  public async Task CoexistsWithReferencePointRows()
  {
    var dir = TempDir();
    try
    {
      var root = GhRoot(
        new()
        {
          ["Client"] = "ACME",
          ["referencePoint"] = new Dictionary<string, object?> { ["units"] = "banana" }, // must not clobber the derived row
        }
      );
      root["referencePointTransform"] = new Dictionary<string, object?>
      {
        ["transform"] = new List<object> { 1L, 0L, 0L, 0L, 0L, 1L, 0L, 0L, 0L, 0L, 1L, 0L, 1.0, 2.0, 3.0, 1L },
      };

      var stats = Migrate(root, dir);

      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);
      bundle.ModelProperties["Client"].Should().Be("ACME");
      AsGroup(bundle.ModelProperties["referencePoint"])["units"].Should().Be("m");
      stats.ModelPropertyRows.Should().Be(2);
      stats.ReferencePoints.Should().Be(1);
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
