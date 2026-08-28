using AwesomeAssertions;
using Speckle.DoubleNumerics;
using Speckle.Objects.Data;
using Speckle.Objects.Geometry;
using Speckle.Objects.Other;
using Speckle.Objects.Utils;
using Speckle.Sdk.BundleMigrator.Migration;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;
using Speckle.Sdk.Models.Instances;
using Speckle.Sdk.Models.Proxies;
using Speckle.Sdk.Pipelines.Receive.Artifacts;

namespace Speckle.Sdk.BundleMigrator.Tests;

/// <summary>
/// Guards <see cref="V3GraphArtifactProducer"/>'s three-plane appearance emission: a v3 proxy ref is preserved
/// on the plane it addressed — a mesh ref becomes geometry-sourced HAS_MATERIAL/HAS_COLOR, an instance-object
/// ref an OBJECT_HAS_* edge, and a layer ref a NODE_HAS_* edge (never flattened onto member geometry).
/// </summary>
public class V3GraphArtifactProducerAppearanceTests
{
  public V3GraphArtifactProducerAppearanceTests()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(Mesh).Assembly);
  }

  private const int Red = unchecked((int)0xFFFF0000);
  private const int Green = unchecked((int)0xFF00FF00);
  private const int Blue = unchecked((int)0xFF0000FF);
  private const int Black = unchecked((int)0xFF000000);

  private static readonly SpeckleApplication TestProducer = new()
  {
    HostApplication = "Test",
    HostApplicationVersion = "1.2.3",
    Slug = "test-connector",
    SpeckleVersion = "999.1.0-alpha.1",
  };

  private static Mesh MeshAt(string appId) =>
    new()
    {
      vertices = new List<double> { 0, 0, 0, 1, 0, 0, 1, 1, 0 },
      faces = new List<int> { 3, 0, 1, 2 },
      units = "m",
      applicationId = appId,
      id = appId,
    };

  private static InstanceProxy Block(string appId, string definitionId) =>
    new()
    {
      definitionId = definitionId,
      transform = Matrix4x4.Identity,
      units = "m",
      maxDepth = 1,
      applicationId = appId,
      id = appId,
    };

  private static RenderMaterialProxy MaterialProxy(string name, int diffuse, List<string> objects) =>
    new()
    {
      value = new RenderMaterial
      {
        name = name,
        diffuse = diffuse,
        applicationId = "mat:" + name,
      },
      objects = objects,
    };

  private static ColorProxy Color(int value, List<string> objects) =>
    new()
    {
      value = value,
      name = null,
      objects = objects,
    };

  private static Stats Migrate(Collection root, string dir)
  {
    using var producer = new V3GraphArtifactProducer(
      new ObjectsArtifactPipeline(dir, "v3", TestProducer),
      new ArtifactHelper()
    );
    return producer.Produce(root);
  }

  [Fact]
  public async Task ProxyRefs_EmitOnThePlaneV3Addressed()
  {
    var dir = Path.Combine(Path.GetTempPath(), "SpeckleV3Appearance", Guid.NewGuid().ToString("N"));
    try
    {
      var layerB = new Collection
      {
        name = "Layer B",
        applicationId = "layerB",
        elements = { MeshAt("mesh2") },
      };
      var layerA = new Collection
      {
        name = "Layer A",
        applicationId = "layerA",
        elements = { MeshAt("mesh1"), layerB },
      };
      var root = new Collection
      {
        name = "root",
        applicationId = "root",
        elements = { layerA, Block("block1", "def1"), Block("block2", "def1"), MeshAt("mesh3") },
      };
      root["renderMaterialProxies"] = new List<object>
      {
        MaterialProxy("paint", unchecked((int)0xFF2244CC), new List<string> { "block1" }),
        MaterialProxy("layerMat", unchecked((int)0xFF66AA22), new List<string> { "layerA" }),
        MaterialProxy("meshMat", unchecked((int)0xFFCC8811), new List<string> { "mesh3" }),
      };
      // ByBlock placeholder targeting the sibling placement: must produce no colour edge at all.
      var byBlock = Color(-1, new List<string> { "block2" });
      byBlock["source"] = "block";
      root["colorProxies"] = new List<object>
      {
        Color(Red, new List<string> { "block1" }),
        Color(Green, new List<string> { "layerA" }),
        Color(Blue, new List<string> { "mesh3" }),
        byBlock,
        Color(Blue, new List<string> { "not-in-graph" }),
      };

      var stats = Migrate(root, dir);
      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);
      var rels = bundle.Relations;

      // Object plane: block1's override colour and painted material; the ByBlock sibling stays clean.
      rels.ColorByObject.Should().HaveCount(1);
      bundle.Nodes[rels.ColorByObject.Single().Value].Argb.Should().Be(Red);
      rels.MaterialByObject.Should().HaveCount(1);
      rels.MaterialByObject.Keys.Should().Equal(rels.ColorByObject.Keys);

      // Node plane: layerA's colour/material ride its CONTAINER node — nothing flooded onto mesh1/mesh2.
      rels.ColorByNode.Should().HaveCount(1);
      var (layerK, layerColK) = rels.ColorByNode.Single();
      bundle.Nodes[layerK].Name.Should().Be("Layer A");
      bundle.Nodes[layerColK].Argb.Should().Be(Green);
      rels.MaterialByNode.Keys.Should().Equal(layerK);

      // Geometry plane: only the directly-referenced mesh.
      rels.ColorByGeometry.Should().HaveCount(1);
      bundle.Nodes[rels.ColorByGeometry.Single().Value].Argb.Should().Be(Blue);
      rels.MaterialByGeometry.Should().HaveCount(1);
      rels.MaterialByInstance.Should().BeEmpty();

      stats.HasColorEdges.Should().Be(1);
      stats.ObjectHasColorEdges.Should().Be(1);
      stats.NodeHasColorEdges.Should().Be(1);
      stats.HasMaterialEdges.Should().Be(1);
      stats.ObjectHasMaterialEdges.Should().Be(1);
      stats.NodeHasMaterialEdges.Should().Be(1);
      stats.ByBlockColorProxies.Should().Be(1);
      stats.SkippedColor.Should().Be(1);
      stats.SkippedMaterial.Should().Be(0);
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
  public async Task PlaceholderBlackMaterial_YieldsToColour_PerPlane()
  {
    var dir = Path.Combine(Path.GetTempPath(), "SpeckleV3Appearance", Guid.NewGuid().ToString("N"));
    try
    {
      var layerA = new Collection
      {
        name = "Layer A",
        applicationId = "layerA",
        elements = { MeshAt("mesh1") },
      };
      var root = new Collection
      {
        name = "root",
        applicationId = "root",
        elements = { layerA, Block("block1", "def1"), MeshAt("mesh2") },
      };
      var targets = new List<string> { "mesh2", "block1", "layerA" };
      root["renderMaterialProxies"] = new List<object> { MaterialProxy("noMaterial", Black, targets) };
      root["colorProxies"] = new List<object> { Color(Red, targets) };

      var stats = Migrate(root, dir);
      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);
      var rels = bundle.Relations;

      rels.MaterialByGeometry.Should().BeEmpty();
      rels.MaterialByObject.Should().BeEmpty();
      rels.MaterialByNode.Should().BeEmpty();
      rels.ColorByGeometry.Should().HaveCount(1);
      rels.ColorByObject.Should().HaveCount(1);
      rels.ColorByNode.Should().HaveCount(1);

      stats.HasMaterialEdges.Should().Be(0);
      stats.ObjectHasMaterialEdges.Should().Be(0);
      stats.NodeHasMaterialEdges.Should().Be(0);
    }
    finally
    {
      if (Directory.Exists(dir))
      {
        Directory.Delete(dir, true);
      }
    }
  }

  private static RhinoObject SolidObject(string appId, bool withMesh = true) =>
    new()
    {
      name = appId,
      type = "Brep",
      units = "m",
      applicationId = appId,
      id = appId,
      rawEncoding = new RawEncoding
      {
        format = RawEncodingFormats.RHINO_3DM,
        contents = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
      },
      displayValue = withMesh ? new List<Base> { MeshAt(appId + "-mesh") } : new List<Base>(),
      properties = new Dictionary<string, object?>(),
    };

  private static InstanceDefinitionProxy Definition(string defId, params string[] memberIds) =>
    new()
    {
      applicationId = defId,
      id = defId,
      name = defId,
      maxDepth = 1,
      objects = memberIds.ToList(),
    };

  [Fact]
  public async Task DefinitionMemberSolid_GetsMaterialAndColour_AlongsideItsMesh()
  {
    var dir = Path.Combine(Path.GetTempPath(), "SpeckleV3Appearance", Guid.NewGuid().ToString("N"));
    try
    {
      var root = new Collection
      {
        name = "root",
        applicationId = "root",
        elements = { SolidObject("member1"), Block("block1", "def1") },
      };
      root["instanceDefinitionProxies"] = new List<object> { Definition("def1", "member1") };
      root["renderMaterialProxies"] = new List<object>
      {
        MaterialProxy("steel", unchecked((int)0xFF2244CC), new List<string> { "member1" }),
      };
      root["colorProxies"] = new List<object> { Color(Red, new List<string> { "member1" }) };

      var stats = Migrate(root, dir);
      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);
      var rels = bundle.Relations;

      // The member's raw solid AND its display mesh both carry the material and the colour.
      rels.MaterialByGeometry.Should().HaveCount(2);
      rels.MaterialByGeometry.Keys.Select(k => bundle.Geometries[k].Type).Should().BeEquivalentTo("3dm", "mesh");
      rels.ColorByGeometry.Keys.Should().BeEquivalentTo(rels.MaterialByGeometry.Keys);

      stats.HasMaterialEdges.Should().Be(2);
      stats.HasColorEdges.Should().Be(2);
      stats.SkippedMaterial.Should().Be(0);
      stats.SkippedColor.Should().Be(0);
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
  public async Task SolidOnlyDefinitionMember_MaterialBindsToTheSolid()
  {
    var dir = Path.Combine(Path.GetTempPath(), "SpeckleV3Appearance", Guid.NewGuid().ToString("N"));
    try
    {
      var root = new Collection
      {
        name = "root",
        applicationId = "root",
        elements = { SolidObject("member1", withMesh: false), Block("block1", "def1") },
      };
      root["instanceDefinitionProxies"] = new List<object> { Definition("def1", "member1") };
      root["renderMaterialProxies"] = new List<object>
      {
        MaterialProxy("steel", unchecked((int)0xFF2244CC), new List<string> { "member1" }),
      };

      var stats = Migrate(root, dir);
      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);
      var rels = bundle.Relations;

      var (solidK, _) = rels.MaterialByGeometry.Should().ContainSingle().Subject;
      bundle.Geometries[solidK].Type.Should().Be("3dm");
      stats.HasMaterialEdges.Should().Be(1);
      stats.SkippedMaterial.Should().Be(0); // previously silently skipped: the member had no appearance target
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
  public async Task StandaloneSolid_GetsMaterial_AlongsideItsMesh()
  {
    var dir = Path.Combine(Path.GetTempPath(), "SpeckleV3Appearance", Guid.NewGuid().ToString("N"));
    try
    {
      // Not referenced by any definition proxy → standalone object: SOLID edge, and the solid is a material target too.
      var root = new Collection
      {
        name = "root",
        applicationId = "root",
        elements = { SolidObject("brep1") },
      };
      root["renderMaterialProxies"] = new List<object>
      {
        MaterialProxy("steel", unchecked((int)0xFF2244CC), new List<string> { "brep1" }),
      };

      var stats = Migrate(root, dir);
      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);
      var rels = bundle.Relations;

      rels.MaterialByGeometry.Should().HaveCount(2);
      rels.MaterialByGeometry.Keys.Select(k => bundle.Geometries[k].Type).Should().BeEquivalentTo("3dm", "mesh");
      stats.HasMaterialEdges.Should().Be(2);
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
  public async Task PlaceholderBlackMaterial_YieldsToColour_OnBothSolidAndMesh()
  {
    var dir = Path.Combine(Path.GetTempPath(), "SpeckleV3Appearance", Guid.NewGuid().ToString("N"));
    try
    {
      var root = new Collection
      {
        name = "root",
        applicationId = "root",
        elements = { SolidObject("member1"), Block("block1", "def1") },
      };
      root["instanceDefinitionProxies"] = new List<object> { Definition("def1", "member1") };
      root["renderMaterialProxies"] = new List<object>
      {
        MaterialProxy("noMaterial", Black, new List<string> { "member1" }),
      };
      root["colorProxies"] = new List<object> { Color(Red, new List<string> { "member1" }) };

      var stats = Migrate(root, dir);
      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);
      var rels = bundle.Relations;

      // The placeholder yields to the real colour on BOTH targets — no mesh/solid appearance split.
      rels.MaterialByGeometry.Should().BeEmpty();
      rels.ColorByGeometry.Should().HaveCount(2);
      rels.ColorByGeometry.Keys.Select(k => bundle.Geometries[k].Type).Should().BeEquivalentTo("3dm", "mesh");
      stats.HasMaterialEdges.Should().Be(0);
      stats.HasColorEdges.Should().Be(2);
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
