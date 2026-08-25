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
/// Guards the object-plane shape of block-definition members: every member is a real object (row, eav, layer)
/// joined to its geometry via DEFINES_MEMBER on the shared member ordinal — or via PLACES for a nested instance —
/// while carrying no render edge of its own (DISPLAY / SOLID / DISPLAY_INSTANCE) and no group membership.
/// </summary>
public class V3GraphArtifactProducerDefinitionMemberTests
{
  public V3GraphArtifactProducerDefinitionMemberTests()
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
    Path.Combine(Path.GetTempPath(), "SpeckleV3DefMembers", Guid.NewGuid().ToString("N"));

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

  private static InstanceDefinitionProxy Definition(string defId, params string[] memberIds) =>
    new()
    {
      applicationId = defId,
      id = defId,
      name = defId,
      maxDepth = 1,
      objects = memberIds.ToList(),
    };

  private static RhinoObject SolidMember(string appId, string name) =>
    new()
    {
      name = name,
      type = "Extrusion",
      units = "m",
      applicationId = appId,
      id = appId,
      rawEncoding = new RawEncoding
      {
        format = RawEncodingFormats.RHINO_3DM,
        contents = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
      },
      displayValue = new List<Base> { MeshAt(appId + "-mesh") },
      properties = new Dictionary<string, object?> { ["Layer"] = "Layer A" },
    };

  // Nothing SGEO can encode and no raw solid: a member that keeps only its object row.
  private static DataObject Unencodable(string appId) =>
    new()
    {
      name = appId,
      applicationId = appId,
      id = appId,
      displayValue = new List<Base>(),
      properties = new Dictionary<string, object?>(),
    };

  private static int ObjIdx(ArtefactBundle bundle, string appId) =>
    bundle.ObjectAppIds.Single(kv => kv.Value == appId).Key;

  [Fact]
  public async Task GeometryMember_IsAnObjectJoinedByOrdinal_WithoutRenderEdges()
  {
    var dir = TempDir();
    try
    {
      var layerA = new Collection
      {
        name = "Layer A",
        applicationId = "layerA",
        elements = { SolidMember("member1", "Extrusion") },
      };
      var root = new Collection
      {
        name = "root",
        applicationId = "root",
        elements = { layerA, Block("block1", "def1") },
      };
      root["instanceDefinitionProxies"] = new List<object> { Definition("def1", "member1") };

      var stats = Migrate(root, dir);
      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);
      var rels = bundle.Relations;

      var memberK = ObjIdx(bundle, "member1");
      var blockK = ObjIdx(bundle, "block1");

      // Object plane: row + eav + layer.
      bundle.Properties[memberK]["name"].Should().Be("Extrusion");
      bundle.Nodes[rels.CollectionByObject[memberK]].Name.Should().Be("Layer A");

      // The join: DEFINES_MEMBER and both DEFINES rows (solid first) share member ordinal 0.
      var (defK, members) = rels.MemberObjectsByDefinition.Should().ContainSingle().Subject;
      members.Should().Equal(memberK);
      rels.MemberOrdByDefinition[defK].Should().Equal(0);
      rels.DefinesOrdByDefinition[defK].Should().Equal(0, 0);
      rels.DefinesByDefinition[defK].Select(k => bundle.Geometries[k].Type).Should().Equal("3dm", "mesh");

      // No render edge of its own — the placement is the only render root.
      rels.ObjectByGeometry().Values.Should().NotContain(memberK);
      rels.SolidByObject.Should().NotContainKey(memberK);
      rels.DisplayInstanceByObject.Should().NotContainKey(memberK);
      rels.DisplayInstanceByObject.Should().ContainKey(blockK);

      stats.Objects.Should().Be(2);
      stats.DefinesMemberEdges.Should().Be(1);
      stats.PlacesEdges.Should().Be(0);
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
  public async Task NestedInstanceMember_IsPlaced_AndAppearanceRidesTheObjectPlane()
  {
    var dir = TempDir();
    try
    {
      // def1 = { nested1 (an instance of def2) }; def2 = { leaf1 }.
      var root = new Collection
      {
        name = "root",
        applicationId = "root",
        elements = { Block("nested1", "def2"), MeshAt("leaf1"), Block("block1", "def1") },
      };
      root["instanceDefinitionProxies"] = new List<object>
      {
        Definition("def1", "nested1"),
        Definition("def2", "leaf1"),
      };
      root["renderMaterialProxies"] = new List<object>
      {
        new RenderMaterialProxy
        {
          value = new RenderMaterial
          {
            name = "paint",
            diffuse = unchecked((int)0xFF2244CC),
            applicationId = "mat:paint",
          },
          objects = new List<string> { "nested1" },
        },
      };

      var stats = Migrate(root, dir);
      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);
      var rels = bundle.Relations;

      var nestedK = ObjIdx(bundle, "nested1");
      var instK = rels.PlacesByObject.Should().ContainKey(nestedK).WhoseValue;

      var def1 = rels.MemberObjectsByDefinition.Single(kv => kv.Value.Contains(nestedK)).Key;
      rels.DefinesInstanceByDefinition[def1].Should().Equal(instK);
      rels.MemberOrdByDefinition[def1].Should().Equal(0);

      // Association only: the nested member never becomes a world-space render root.
      rels.DisplayInstanceByObject.Should().NotContainKey(nestedK);

      // Its material lands on the object plane, like a placed instance's.
      rels.MaterialByObject.Should().ContainKey(nestedK);
      rels.MaterialByGeometry.Should().BeEmpty();

      stats.DefinesMemberEdges.Should().Be(2); // nested1 in def1, leaf1 in def2
      stats.PlacesEdges.Should().Be(1);
      stats.ObjectHasMaterialEdges.Should().Be(1);
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
  public async Task MemberOrdinal_IsTheProxyIndex_AndUnencodableMemberKeepsItsObject()
  {
    var dir = TempDir();
    try
    {
      var layerA = new Collection
      {
        name = "Layer A",
        applicationId = "layerA",
        elements = { Unencodable("ghost1"), MeshAt("leaf1") },
      };
      var root = new Collection
      {
        name = "root",
        applicationId = "root",
        elements = { layerA, Block("block1", "def1") },
      };
      root["instanceDefinitionProxies"] = new List<object> { Definition("def1", "ghost1", "leaf1") };

      var stats = Migrate(root, dir);
      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);
      var rels = bundle.Relations;

      var ghostK = ObjIdx(bundle, "ghost1");
      var leafK = ObjIdx(bundle, "leaf1");

      // The ghost keeps its object row, eav and layer — but nothing to join to, so no DEFINES_MEMBER.
      bundle.Properties[ghostK]["name"].Should().Be("ghost1");
      bundle.Nodes[rels.CollectionByObject[ghostK]].Name.Should().Be("Layer A");
      var (defK, members) = rels.MemberObjectsByDefinition.Should().ContainSingle().Subject;
      members.Should().Equal(leafK);

      // leaf1 is the second entry of the proxy, so its ordinal is 1 even though ghost1 emitted nothing.
      rels.MemberOrdByDefinition[defK].Should().Equal(1);
      rels.DefinesOrdByDefinition[defK].Should().Equal(1);

      stats.SkippedDefines.Should().Be(1);
      stats.DefinesMemberEdges.Should().Be(1);
      stats.Notes.Should().ContainSingle(n => n.Contains("ghost1"));
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
  public async Task Groups_ExcludeDefinitionMembers()
  {
    var dir = TempDir();
    try
    {
      var root = new Collection
      {
        name = "root",
        applicationId = "root",
        elements = { MeshAt("member1"), MeshAt("member2"), MeshAt("mesh1"), Block("block1", "def1") },
      };
      root["instanceDefinitionProxies"] = new List<object> { Definition("def1", "member1", "member2") };
      root["groupProxies"] = new List<object>
      {
        new GroupProxy
        {
          name = "mixed",
          applicationId = "grp1",
          id = "grp1",
          objects = new List<string> { "member1", "mesh1" },
        },
        new GroupProxy
        {
          name = "membersOnly",
          applicationId = "grp2",
          id = "grp2",
          objects = new List<string> { "member1", "member2" },
        },
      };

      var stats = Migrate(root, dir);
      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);
      var rels = bundle.Relations;

      rels.GroupsByObject.Keys.Should().Equal(ObjIdx(bundle, "mesh1"));
      stats.Groups.Should().Be(1); // the members-only group leaves no container behind
      stats.InGroupEdges.Should().Be(1);
      stats.SkippedGroup.Should().Be(0); // exclusion is deliberate, not a dangling ref
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
