using System.Globalization;
using AwesomeAssertions;
using Speckle.Objects.Data;
using Speckle.Objects.Geometry;
using Speckle.Objects.Other;
using Speckle.Objects.Utils;
using Speckle.Sdk;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;
using Speckle.Sdk.Pipelines;
using Speckle.Sdk.Pipelines.Receive.Artifacts;
using Speckle.Sdk.Pipelines.Send.Artifacts;

namespace Speckle.Objects.Tests.Unit.Utils;

/// <summary>
/// Round-trips the Speckle 4.0 artefact bundle: write via <see cref="ObjectsArtifactPipeline"/> → read via
/// <see cref="ObjectsArtifactReader"/> → assert the reconstructed Base/Collection graph matches. Guards the
/// connector receive path (layers, displayValue meshes via SGEO, 3dm SOLID blobs, material proxies).
/// </summary>
public class ArtifactRoundTripTests
{
  // Complete() requires a producer — meta names whoever wrote the bundle.
  private static readonly SpeckleApplication TestProducer = new()
  {
    HostApplication = "Test",
    HostApplicationVersion = "1.2.3",
    Slug = "test-connector",
    SpeckleVersion = "999.1.0-alpha.1",
  };

  [Fact]
  public async Task RoundTrip_RebuildsLayersMeshesAndMaterials()
  {
    var dir = Path.Combine(Path.GetTempPath(), "SpeckleArtifactRoundTrip", Guid.NewGuid().ToString("N"));
    try
    {
      using (var pipeline = new ObjectsArtifactPipeline(dir, "rt", TestProducer))
      {
        int collK = pipeline.AddCollection("layer-1", "Layer 1", null, "Layer");
        int objK = pipeline.InternObject("obj-1");
        pipeline.AddProperties(
          "obj-1",
          new Dictionary<string, object?> { ["custom"] = "value" },
          new[] { new KeyValuePair<string, object?>("name", "Box"), new KeyValuePair<string, object?>("units", "m") }
        );
        var mesh = new Mesh
        {
          vertices = new List<double> { 0, 0, 0, 1, 0, 0, 1, 1, 0 },
          faces = new List<int> { 3, 0, 1, 2 },
          units = "m",
        };
        int gK = pipeline.AddGeometry("obj-1:g0", mesh);
        pipeline.Display(objK, gK, 0);
        pipeline.InCollection(objK, collK, 0);
        int matK = pipeline.AddMaterial(
          "mat-1",
          "Concrete - Cast In Situ",
          unchecked((int)0xFFAABBCC),
          1.0,
          0.0,
          0.5,
          unchecked((int)0xFF102030),
          1.52
        );
        pipeline.HasMaterial(gK, matK);
        pipeline.SetProducer(TestProducer);
        pipeline.Complete();
      }

      var reader = new ObjectsArtifactReader();
      var root = (Collection)await reader.ReadAsync(dir, new ArtifactReceiveOptions(PreferSolids: true), default);

      var layer = root.elements.OfType<Collection>().Single();
      layer.name.Should().Be("Layer 1");

      var obj = layer.elements.OfType<DataObject>().Single();
      obj.applicationId.Should().Be("obj-1");
      obj.name.Should().Be("Box");
      var meshes = obj.displayValue.OfType<Mesh>().ToList();
      meshes.Should().HaveCount(1);
      meshes[0].vertices.Should().HaveCount(9); // 3 verts × xyz, round-tripped through SGEO

      // List<object>, not List<Base> — the reader stores proxies the way the v1 deserializer materialises them,
      // so the host-side RootObjectUnpacker's `root[key] as List<object>` cast succeeds.
      var materialProxies = root["renderMaterialProxies"] as List<object>;
      materialProxies.Should().NotBeNull();
      materialProxies!.Should().HaveCount(1);
      // the authored material name must survive the node `name` column, not fall back to the "material" placeholder
      var roundTripped = materialProxies.OfType<RenderMaterialProxy>().Single().value;
      roundTripped.name.Should().Be("Concrete - Cast In Situ");
      // ENG-8791: emissive rides the node `emissive` column; ior comes back as the v1-convention dynamic prop.
      roundTripped.emissive.Should().Be(unchecked((int)0xFF102030));
      roundTripped["ior"].Should().Be(1.52);
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
  public async Task RoundTrip_SolidBlob_PreferSolidsVsMeshesOnly()
  {
    var dir = Path.Combine(Path.GetTempPath(), "SpeckleArtifactRoundTrip", Guid.NewGuid().ToString("N"));
    var solidBytes = new byte[] { 9, 8, 7, 6, 5, 4 };
    try
    {
      using (var pipeline = new ObjectsArtifactPipeline(dir, "rt", TestProducer))
      {
        int objK = pipeline.InternObject("solid-1");
        pipeline.AddProperties(
          "solid-1",
          new Dictionary<string, object?>(),
          new[] { new KeyValuePair<string, object?>("units", "m"), new KeyValuePair<string, object?>("type", "Brep") }
        );
        int solidK = pipeline.AddRawGeometry("solid-1:solid", solidBytes, "3dm");
        pipeline.Solid(objK, solidK, 0);
        pipeline.Complete();
      }

      var reader = new ObjectsArtifactReader();

      // Rhino (PreferSolids = true): rebuilt as a RhinoObject carrying the verbatim 3dm rawEncoding.
      var rootSolids = (Collection)await reader.ReadAsync(dir, new ArtifactReceiveOptions(PreferSolids: true), default);
      var rhinoObj = Flatten(rootSolids).OfType<RhinoObject>().Single();
      rhinoObj.rawEncoding.Should().NotBeNull();
      rhinoObj.rawEncoding!.format.Should().Be("3dm");
      Convert.FromBase64String(rhinoObj.rawEncoding.contents).Should().Equal(solidBytes);

      // Revit (PreferSolids = false): the 3dm blob is not accepted and the object has no DISPLAY meshes — since
      // #520 the reader skips such objects entirely rather than fabricating an empty-displayValue DataObject the
      // v1 converter pipeline has no path for (see BuildGeometryObject's null return).
      var rootMeshes = (Collection)
        await reader.ReadAsync(dir, new ArtifactReceiveOptions(PreferSolids: false), default);
      Flatten(rootMeshes).OfType<RhinoObject>().Should().BeEmpty();
      Flatten(rootMeshes).Where(b => b.applicationId == "solid-1").Should().BeEmpty();
    }
    finally
    {
      if (Directory.Exists(dir))
      {
        Directory.Delete(dir, true);
      }
    }
  }

  /// <summary>
  /// ENG-9110 / ENG-8851: a block-definition MEMBER keeps an object row carrying the ordinary object-sourced
  /// IN_COLLECTION (that is how its layer survives) plus an <c>@speckle.geometry_k</c> eav stamp joining that row
  /// back to the geometry its definition owns via DEFINES. The member has no DISPLAY edge — it renders only through
  /// a placed instance [ENG-8782] — and the whole scheme rests on two properties of that shape, both asserted here:
  /// the stamp must survive the parquet round trip (it is the only route from a definition's geometry back to the
  /// member), and a render-less carrier must NOT materialise as a scene object.
  /// </summary>
  [Fact]
  public async Task RoundTrip_DefinitionMemberCarriesItsLayerWithoutBecomingASceneObject()
  {
    var dir = Path.Combine(Path.GetTempPath(), "SpeckleArtifactRoundTrip", Guid.NewGuid().ToString("N"));
    int layerBK,
      memberGeoK,
      memberSolidK;
    try
    {
      using (var pipeline = new ObjectsArtifactPipeline(dir, "rt", TestProducer))
      {
        int layerAK = pipeline.AddCollection("layer-a", "Layer A", null, "Layer");
        layerBK = pipeline.AddCollection("layer-b", "Layer B", null, "Layer");

        // the placement, on Layer A — a real scene object
        int defK = pipeline.AddDefinition("def-1", "Frame");
        int placementK = pipeline.InternObject("inst-1");
        pipeline.AddProperties("inst-1", new Dictionary<string, object?>(), Units("m"));
        pipeline.DisplayInstance(placementK, pipeline.AddInstance("inst-1", defK, Identity(), "m"), 0);
        pipeline.InCollection(placementK, layerAK, 0);

        // The member, on Layer B: object row + IN_COLLECTION + the geometry stamp, and NO display edge. A Rhino
        // member owns TWO geometry Ks — the lossless 3dm solid and its display mesh, which receive chooses between
        // per member — so the stamp is a comma-joined list and both Ks must lead back here.
        memberSolidK = pipeline.AddRawGeometry("member-1:solid", [1, 2, 3], "3dm");
        memberGeoK = pipeline.AddGeometry("member-1:g0", Triangle());
        pipeline.Defines(defK, memberSolidK, 0);
        pipeline.Defines(defK, memberGeoK, 0); // same member ordinal — solid + its display shadow
        pipeline.InCollection(pipeline.InternObject("member-1"), layerBK, 0);
        pipeline.AddProperties(
          "member-1",
          new Dictionary<string, object?>(),
          [
            new("units", "m"),
            new("@speckle.geometry_k", string.Create(CultureInfo.InvariantCulture, $"{memberSolidK},{memberGeoK}")),
          ]
        );

        pipeline.AddSceneView(new SceneView(0, "Default", true, new[] { SceneViewKey.Rel(RelKind.InCollection) }));
        pipeline.Complete();
      }

      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);
      int memberObjK = bundle.ObjectAppIds.Single(kv => kv.Value == "member-1").Key;

      // 1. The stamp survives, and a dotted eav path comes back NESTED — the shape every reader of it must expect.
      //    Note what eav does to the VALUE: a multi-K list isn't numeric under the invariant culture (NumberStyles
      //    .Float admits no thousands separator) so it stays a string, but a SINGLE-K stamp is coerced to a number
      //    and returns as a double. Any reader has to accept both — see DefinitionMemberStamps.ParseKs.
      var stamps = bundle.Properties[memberObjK]["@speckle"].Should().BeOfType<Dictionary<string, object?>>().Subject;
      stamps["geometry_k"].Should().Be(string.Create(CultureInfo.InvariantCulture, $"{memberSolidK},{memberGeoK}"));

      // 2. the member's layer is readable through the ordinary scene-tree map, no new relation involved.
      bundle.Relations.CollectionByObject[memberObjK].Should().Be(layerBK);
      SceneViewResolver.Segments(bundle, memberObjK).Should().Equal("Layer B");

      // 3. the carrier has no render edge, so the v1 reader drops it rather than emitting a phantom scene object.
      //    This is the invariant the whole approach depends on (see DefinitionMemberStamps in the connectors repo).
      bundle.Relations.DisplayByObject(memberObjK).Should().BeNull();
      var reader = new ObjectsArtifactReader();
      var root = (Collection)await reader.ReadAsync(dir, new ArtifactReceiveOptions(PreferSolids: true), default);
      Flatten(root).Where(b => b.applicationId == "member-1").Should().BeEmpty();
    }
    finally
    {
      if (Directory.Exists(dir))
      {
        Directory.Delete(dir, true);
      }
    }
  }

  private static KeyValuePair<string, object?>[] Units(string units) =>
    [new KeyValuePair<string, object?>("units", units)];

  private static double[] Identity() => [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];

  private static Mesh Triangle() =>
    new()
    {
      vertices = new List<double> { 0, 0, 0, 1, 0, 0, 1, 1, 0 },
      faces = new List<int> { 3, 0, 1, 2 },
      units = "m",
    };

  private static IEnumerable<Base> Flatten(Base b)
  {
    yield return b;
    if (b is Collection c)
    {
      foreach (var element in c.elements)
      {
        foreach (var nested in Flatten(element))
        {
          yield return nested;
        }
      }
    }
  }
}
