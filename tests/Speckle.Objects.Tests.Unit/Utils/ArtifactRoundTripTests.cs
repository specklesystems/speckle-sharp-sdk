using AwesomeAssertions;
using Speckle.Objects.Data;
using Speckle.Objects.Geometry;
using Speckle.Objects.Utils;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;

namespace Speckle.Objects.Tests.Unit.Utils;

/// <summary>
/// Round-trips the Speckle 4.0 artefact bundle: write via <see cref="ObjectsArtifactPipeline"/> → read via
/// <see cref="ObjectsArtifactReader"/> → assert the reconstructed Base/Collection graph matches. Guards the
/// connector receive path (layers, displayValue meshes via SGEO, 3dm SOLID blobs, material proxies).
/// </summary>
public class ArtifactRoundTripTests
{
  [Fact]
  public async Task RoundTrip_RebuildsLayersMeshesAndMaterials()
  {
    var dir = Path.Combine(Path.GetTempPath(), "SpeckleArtifactRoundTrip", Guid.NewGuid().ToString("N"));
    try
    {
      using (var pipeline = new ObjectsArtifactPipeline(dir, "rt"))
      {
        int collK = pipeline.AddCollection("layer-1", "Layer 1", null, "Layer");
        int objK = pipeline.InternObject("obj-1");
        pipeline.AddProperties(
          "obj-1",
          new Dictionary<string, object?> { ["custom"] = "value" },
          new[]
          {
            new KeyValuePair<string, object?>("name", "Box"),
            new KeyValuePair<string, object?>("units", "m"),
          }
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
        int matK = pipeline.AddMaterial("mat-1", unchecked((int)0xFFAABBCC), 1.0, 0.0, 0.5);
        pipeline.HasMaterial(gK, matK);
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

      var materialProxies = root["renderMaterialProxies"] as List<Base>;
      materialProxies.Should().NotBeNull();
      materialProxies!.Should().HaveCount(1);
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
      using (var pipeline = new ObjectsArtifactPipeline(dir, "rt"))
      {
        int objK = pipeline.InternObject("solid-1");
        pipeline.AddProperties(
          "solid-1",
          new Dictionary<string, object?>(),
          new[]
          {
            new KeyValuePair<string, object?>("units", "m"),
            new KeyValuePair<string, object?>("type", "Brep"),
          }
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

      // Revit (PreferSolids = false): no 3dm, rebuilt as a plain DataObject (meshes only).
      var rootMeshes = (Collection)await reader.ReadAsync(dir, new ArtifactReceiveOptions(PreferSolids: false), default);
      Flatten(rootMeshes).OfType<RhinoObject>().Should().BeEmpty();
      Flatten(rootMeshes).OfType<DataObject>().Should().ContainSingle(d => d.applicationId == "solid-1");
    }
    finally
    {
      if (Directory.Exists(dir))
      {
        Directory.Delete(dir, true);
      }
    }
  }

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
