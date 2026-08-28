using AwesomeAssertions;
using Speckle.Objects.Data;
using Speckle.Objects.Geometry;
using Speckle.Objects.Other;
using Speckle.Objects.Utils;
using Speckle.Sdk.BundleMigrator.Migration;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;
using Speckle.Sdk.Models.Instances;
using Speckle.Sdk.Pipelines.Receive.Artifacts;

namespace Speckle.Sdk.BundleMigrator.Tests;

/// <summary>
/// Guards ENG-9385: a v3 DataObject whose displayValue holds BrepX/ExtrusionX/SubDX (as Grasshopper sent them) yields
/// both the raw solid and the display mesh per nested item — SOLID + DISPLAY on a standalone object, DEFINES on a
/// definition member — with appearance bound to both.
/// </summary>
public class V3GraphArtifactProducerNestedSolidTests
{
  public V3GraphArtifactProducerNestedSolidTests()
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

  private static Stats Migrate(Collection root, string dir)
  {
    using var producer = new V3GraphArtifactProducer(
      new ObjectsArtifactPipeline(dir, "v3", TestProducer),
      new ArtifactHelper()
    );
    return producer.Produce(root);
  }

  private static string TempDir() =>
    Path.Combine(Path.GetTempPath(), "SpeckleV3NestedSolids", Guid.NewGuid().ToString("N"));

  private static Mesh MeshAt(string appId) =>
    new()
    {
      vertices = new List<double> { 0, 0, 0, 1, 0, 0, 1, 1, 0 },
      faces = new List<int> { 3, 0, 1, 2 },
      units = "m",
      applicationId = appId,
      id = appId,
    };

  private static BrepX BrepXAt(string appId, string format = RawEncodingFormats.RHINO_3DM, Mesh? display = null) =>
    new()
    {
      units = "m",
      applicationId = appId,
      id = appId,
      encodedValue = new RawEncoding { format = format, contents = Convert.ToBase64String(new byte[] { 1, 2, 3 }) },
      displayValue = display is null ? new List<Mesh>() : new List<Mesh> { display },
    };

  private static DataObject Host(string appId, params Base[] display) =>
    new()
    {
      name = appId,
      applicationId = appId,
      id = appId,
      displayValue = display.ToList(),
      properties = new Dictionary<string, object?>(),
    };

  private static Collection Root(params Base[] elements)
  {
    var root = new Collection { name = "root", applicationId = "root" };
    root.elements.AddRange(elements);
    return root;
  }

  private static int ObjIdx(ArtefactBundle bundle, string appId) =>
    bundle.ObjectAppIds.Single(kv => kv.Value == appId).Key;

  private static IEnumerable<string> Types(ArtefactBundle bundle, IEnumerable<int> geomKs) =>
    geomKs.Select(k => bundle.Geometries[k].Type);

  [Fact]
  public async Task StandaloneHost_GetsOneSolidAndOneDisplayPerNestedBrepX()
  {
    var dir = TempDir();
    try
    {
      var host = Host("host", BrepXAt("bx1", display: MeshAt("bx1-mesh")), BrepXAt("bx2", display: MeshAt("bx2-mesh")));
      var root = Root(host);
      root["renderMaterialProxies"] = new List<object>
      {
        new RenderMaterialProxy
        {
          value = new RenderMaterial
          {
            name = "steel",
            diffuse = unchecked((int)0xFF2244CC),
            applicationId = "mat:steel",
          },
          objects = new List<string> { "host" },
        },
      };

      var stats = Migrate(root, dir);
      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);
      var rels = bundle.Relations;
      var hostK = ObjIdx(bundle, "host");

      Types(bundle, bundle.Geometries.Keys).Should().BeEquivalentTo("3dm", "3dm", "mesh", "mesh");
      Types(bundle, rels.SolidByObject[hostK]).Should().Equal("3dm", "3dm");
      rels.ObjectByGeometry().Should().HaveCount(2).And.OnlyContain(kv => kv.Value == hostK);

      // Both solids and both meshes carry the host's material.
      rels.MaterialByGeometry.Should().HaveCount(4);

      stats.Solids.Should().Be(2);
      stats.DisplayEdges.Should().Be(2);
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
  public async Task MemberHost_RidesDefines_WithSolidsAndMeshesOnOneOrdinal()
  {
    var dir = TempDir();
    try
    {
      var root = Root(
        Host("member1", BrepXAt("bx1", display: MeshAt("bx1-mesh"))),
        new InstanceProxy
        {
          definitionId = "def1",
          transform = Speckle.DoubleNumerics.Matrix4x4.Identity,
          units = "m",
          maxDepth = 1,
          applicationId = "block1",
          id = "block1",
        }
      );
      root["instanceDefinitionProxies"] = new List<object>
      {
        new InstanceDefinitionProxy
        {
          applicationId = "def1",
          id = "def1",
          name = "def1",
          maxDepth = 1,
          objects = new List<string> { "member1" },
        },
      };

      var stats = Migrate(root, dir);
      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);
      var rels = bundle.Relations;
      var memberK = ObjIdx(bundle, "member1");

      var (defK, geomKs) = rels.DefinesByDefinition.Should().ContainSingle().Subject;
      Types(bundle, geomKs).Should().Equal("3dm", "mesh");
      rels.DefinesOrdByDefinition[defK].Should().Equal(0, 0);
      rels.MemberObjectsByDefinition[defK].Should().Equal(memberK);

      rels.SolidByObject.Should().NotContainKey(memberK);
      rels.ObjectByGeometry().Should().BeEmpty();
      stats.Solids.Should().Be(1);
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
  public async Task NestedBrepX_WithoutDisplayMesh_YieldsSolidOnly()
  {
    var dir = TempDir();
    try
    {
      var stats = Migrate(Root(Host("host", BrepXAt("bx1"))), dir);
      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);
      var rels = bundle.Relations;
      var hostK = ObjIdx(bundle, "host");

      Types(bundle, bundle.Geometries.Keys).Should().Equal("3dm");
      rels.SolidByObject[hostK].Should().HaveCount(1);
      rels.ObjectByGeometry().Should().BeEmpty();
      stats.Solids.Should().Be(1);
      stats.DisplayEdges.Should().Be(0);
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
  public async Task NestedBrepX_WithUnmigratableFormat_YieldsMeshOnly()
  {
    var dir = TempDir();
    try
    {
      var stats = Migrate(Root(Host("host", BrepXAt("bx1", RawEncodingFormats.ACAD_DWG, MeshAt("bx1-mesh")))), dir);
      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);
      var rels = bundle.Relations;
      var hostK = ObjIdx(bundle, "host");

      Types(bundle, bundle.Geometries.Keys).Should().Equal("mesh");
      rels.SolidByObject.Should().NotContainKey(hostK);
      rels.ObjectByGeometry().Should().ContainSingle().Which.Value.Should().Be(hostK);
      stats.Solids.Should().Be(0);
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
  public async Task NestedBrepX_WithEmptyDisplayMesh_DropsTheMesh_KeepsTheSolid()
  {
    var dir = TempDir();
    try
    {
      var empty = new Mesh
      {
        vertices = new List<double>(),
        faces = new List<int>(),
        units = "m",
        applicationId = "bx1-mesh",
        id = "bx1-mesh",
      };
      var stats = Migrate(Root(Host("host", BrepXAt("bx1", display: empty))), dir);
      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);

      // The unwrapped mesh still runs the empty-mesh guard (it would otherwise write NaN into viewer.idx).
      Types(bundle, bundle.Geometries.Keys).Should().Equal("3dm");
      stats.Solids.Should().Be(1);
      stats.DisplayEdges.Should().Be(0);
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
