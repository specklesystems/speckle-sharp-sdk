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
/// Guards the v3 Grasshopper `topology` passthrough: the connector's dynamic data-tree string on each
/// collection (e.g. <c>0-1 0;0-1</c>) lands verbatim on the container node's <c>gh_topology</c> column,
/// while the explicit nulls it writes on non-tree collections stay null.
/// </summary>
public class V3GraphArtifactProducerGhTopologyTests
{
  public V3GraphArtifactProducerGhTopologyTests()
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

  private static Collection MakeCollection(string name, string? topology, params Base[] elements)
  {
    var col = new Collection { name = name, applicationId = name };
    col["topology"] = topology;
    col.elements.AddRange(elements);
    return col;
  }

  private static Stats Migrate(Collection root, string dir)
  {
    using var producer = new V3GraphArtifactProducer(
      new ObjectsArtifactPipeline(dir, "v3", TestProducer),
      new ArtifactHelper()
    );
    return producer.Produce(root);
  }

  private static string TempDir() =>
    Path.Combine(Path.GetTempPath(), "SpeckleV3GhTopology", Guid.NewGuid().ToString("N"));

  private static ArtefactNode NodeByName(ArtefactBundle bundle, string name) =>
    bundle.Nodes.Values.Single(n => n.Name == name);

  [Fact]
  public async Task TopologyStrings_LandVerbatimOnContainerNodes()
  {
    var dir = TempDir();
    try
    {
      // Mirrors the real GH shape: root → tree collection → nested tree collection → objects.
      var inner = MakeCollection("Stacked Tower", "0-1", UnitMesh("m1"));
      var outer = MakeCollection("Sub-Collection 1", "0-1 0;0-1", UnitMesh("m2"), inner);
      var root = new Collection { name = "root", applicationId = "root", elements = { outer } };

      var stats = Migrate(root, dir);

      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);
      NodeByName(bundle, "Sub-Collection 1").GhTopology.Should().Be("0-1 0;0-1");
      NodeByName(bundle, "Stacked Tower").GhTopology.Should().Be("0-1");
      stats.GhTopologies.Should().Be(2);
      stats.Notes.Should().BeEmpty();
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
  public async Task NullOrAbsentTopology_StaysNull()
  {
    var dir = TempDir();
    try
    {
      // GH writes an explicit null on collections without an authored tree; other producers omit the key.
      var explicitNull = MakeCollection("L1", null, UnitMesh("m1"));
      var noKey = new Collection { name = "L2", applicationId = "L2", elements = { UnitMesh("m2") } };
      var root = new Collection { name = "root", applicationId = "root", elements = { explicitNull, noKey } };

      var stats = Migrate(root, dir);

      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);
      NodeByName(bundle, "L1").GhTopology.Should().BeNull();
      NodeByName(bundle, "L2").GhTopology.Should().BeNull();
      stats.GhTopologies.Should().Be(0);
      stats.Notes.Should().BeEmpty();
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
  public async Task NonStringTopology_SkippedWithNote()
  {
    var dir = TempDir();
    try
    {
      var col = MakeCollection("odd", null, UnitMesh("m1"));
      col["topology"] = 42L;
      var root = new Collection { name = "root", applicationId = "root", elements = { col } };

      var stats = Migrate(root, dir);

      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);
      NodeByName(bundle, "odd").GhTopology.Should().BeNull();
      stats.GhTopologies.Should().Be(0);
      stats.Notes.Should().ContainSingle(n => n.Contains("topology"));
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
