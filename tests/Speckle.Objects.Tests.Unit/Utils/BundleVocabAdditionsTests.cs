using AwesomeAssertions;
using Speckle.Objects.Geometry;
using Speckle.Objects.Utils;
using Speckle.Sdk;
using Speckle.Sdk.Pipelines.Receive.Artifacts;

namespace Speckle.Objects.Tests.Unit.Utils;

/// <summary>
/// Round-trips the post-v5 vocabulary additions: member rels PLACES(24)/DEFINES_MEMBER(25), object-plane
/// appearance OBJECT_HAS_MATERIAL(26)/OBJECT_HAS_COLOR(27), and the optional purpose files
/// <c>eav.model.parquet</c> / <c>eav.property_set_definitions.parquet</c>. Also guards the lazy-optional
/// contract: a pipeline that never touches them ships NO file.
/// </summary>
public class BundleVocabAdditionsTests
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

  [Fact]
  public async Task MemberRels_ModelAndPropertySetFiles_RoundTrip()
  {
    var dir = Path.Combine(Path.GetTempPath(), "SpeckleVocabAdditions", Guid.NewGuid().ToString("N"));
    try
    {
      using (var pipeline = new ObjectsArtifactPipeline(dir, "va", TestProducer))
      {
        // A definition with one geometry member (object 'member-1', member ord 0) and one nested
        // placement member ('member-2' PLACES instance node) — the stamp-replacement shape.
        int defK = pipeline.AddDefinition("chair", "Chair");
        int gK = pipeline.AddGeometry("chair:g0", UnitTriangle());
        pipeline.Defines(defK, gK, 0);
        int memberObjK = pipeline.InternObject("member-1");
        pipeline.AddProperties("member-1", new Dictionary<string, object?> { ["layer"] = "Details" });
        pipeline.DefinesMember(defK, memberObjK, 0);

        int nestedDefK = pipeline.AddDefinition("cushion", "Cushion");
        int nestedInstK = pipeline.AddInstance(
          "chair/cushion-0",
          nestedDefK,
          new double[] { 1, 0, 0, 0.5, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 },
          "m"
        );
        pipeline.DefinesInstance(defK, nestedInstK, 1);
        int nestedMemberK = pipeline.InternObject("member-2");
        pipeline.Places(nestedMemberK, nestedInstK);
        pipeline.DefinesMember(defK, nestedMemberK, 1);

        // Object-plane appearance (fill semantics — successors of the ord-tag era).
        int topObjK = pipeline.InternObject("chair-1");
        int instK = pipeline.AddInstance(
          "chair-1:placement",
          defK,
          new double[] { 1, 0, 0, 2, 0, 1, 0, 1, 0, 0, 1, 0, 0, 0, 0, 1 },
          "m"
        );
        pipeline.DisplayInstance(topObjK, instK, 0);
        int matK = pipeline.AddMaterial("fabric", "Fabric, Blue", unchecked((int)0xFF2244CC), 1.0, 0.0, 0.8);
        pipeline.ObjectHasMaterial(topObjK, matK);
        int colK = pipeline.AddColor(unchecked((int)0xFFE03A2F));
        pipeline.ObjectHasColor(topObjK, colK);

        // Container appearance (rels 28/29): a CONTAINER node carrying material + colour.
        int tagK = pipeline.AddContainer("tag:Interiors", "Interiors", null, "Collection");
        pipeline.NodeHasMaterial(tagK, matK);
        pipeline.NodeHasColor(tagK, colK);

        // Model-scoped attributes: the model placement record + a document fact.
        pipeline.AddModelProperty("modelPlacement.transform", "1,0,0,30.5,0,1,0,12.2,0,0,1,0,0,0,0,1");
        pipeline.AddModelProperty("modelPlacement.default", "projectBasePoint");
        pipeline.AddModelProperty("modelPlacement.appliedToGeometry", true);
        pipeline.AddModelProperty("projectInformation.buildingHeight", 42.5, "m");
        pipeline.AddModelProperty("projectInformation.isMetric", true);

        // One property-set schema, three fields in authored order (row order IS field order).
        pipeline.AddPropertySetDefinition(
          "Pipe Data",
          "ps_hash1",
          "Slope",
          "SLOPE_BUCKET",
          "Real",
          defaultDouble: 0.0,
          unit: "%",
          description: "Design slope",
          setDescription: "Hydraulic pipe data"
        );
        pipeline.AddPropertySetDefinition("Pipe Data", "ps_hash1", "Service", "SERVICE_BUCKET", "Text", "Supply");
        pipeline.AddPropertySetDefinition(
          "Pipe Data",
          "ps_hash1",
          "Insulated",
          null,
          "TrueFalse",
          defaultBoolean: false
        );

        pipeline.Complete();
      }

      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);

      // Member rels: (definition, ord) joins for the geometry member; PLACES for the nested one.
      var rels = bundle.Relations;
      int def = rels.MemberObjectsByDefinition.Keys.Single();
      rels.MemberObjectsByDefinition[def].Should().HaveCount(2);
      rels.MemberOrdByDefinition[def].Should().Equal(0, 1);
      rels.PlacesByObject.Should().HaveCount(1);
      rels.DefinesByDefinition[def].Should().HaveCount(1);
      rels.DefinesOrdByDefinition[def].Should().Equal(0);

      // Object-plane appearance landed in the object-keyed maps.
      rels.MaterialByObject.Should().HaveCount(1);
      rels.MaterialByNode.Should().HaveCount(1);
      rels.ColorByNode.Should().HaveCount(1);
      rels.ColorByObject.Should().HaveCount(1);
      rels.MaterialByGeometry.Should().BeEmpty();
      rels.ColorByGeometry.Should().BeEmpty();

      // Model file: nested by dotted path, values coalesced per type.
      var placement = (Dictionary<string, object?>)bundle.ModelProperties["modelPlacement"]!;
      placement["transform"].Should().Be("1,0,0,30.5,0,1,0,12.2,0,0,1,0,0,0,0,1");
      placement["default"].Should().Be("projectBasePoint");
      placement["appliedToGeometry"].Should().Be(true);
      var projInfo = (Dictionary<string, object?>)bundle.ModelProperties["projectInformation"]!;
      projInfo["buildingHeight"].Should().Be(42.5);
      projInfo["isMetric"].Should().Be(true);

      // Property-set schema rows: field_bucket_id is the rebind join key; row order is authored field order.
      bundle.PropertySetDefinitions.Should().HaveCount(3);
      bundle.PropertySetDefinitions.Select(f => f.FieldName).Should().Equal("Slope", "Service", "Insulated");
      var slope = bundle.PropertySetDefinitions[0];
      slope.SetName.Should().Be("Pipe Data");
      slope.SetKey.Should().Be("ps_hash1");
      slope.SetDescription.Should().Be("Hydraulic pipe data");
      slope.FieldBucketId.Should().Be("SLOPE_BUCKET");
      slope.DataType.Should().Be("Real");
      slope.DefaultDouble.Should().Be(0.0);
      slope.Unit.Should().Be("%");
      var service = bundle.PropertySetDefinitions[1];
      service.FieldBucketId.Should().Be("SERVICE_BUCKET");
      service.DefaultString.Should().Be("Supply");
      var insulated = bundle.PropertySetDefinitions[2];
      insulated.FieldBucketId.Should().BeNull();
      insulated.DefaultBoolean.Should().Be(false);
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
  public async Task OptionalFiles_AbsentWhenUnused()
  {
    var dir = Path.Combine(Path.GetTempPath(), "SpeckleVocabAdditions", Guid.NewGuid().ToString("N"));
    try
    {
      using (var pipeline = new ObjectsArtifactPipeline(dir, "min", TestProducer))
      {
        int objK = pipeline.InternObject("obj-1");
        pipeline.AddProperties(
          "obj-1",
          new Dictionary<string, object?>(),
          new[] { new KeyValuePair<string, object?>("units", "m") }
        );
        int gK = pipeline.AddGeometry("obj-1:g0", UnitTriangle());
        pipeline.Display(objK, gK, 0);
        pipeline.Complete();
      }

      File.Exists(Path.Combine(dir, "min.eav.model.parquet")).Should().BeFalse();
      File.Exists(Path.Combine(dir, "min.eav.property_set_definitions.parquet")).Should().BeFalse();

      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);
      bundle.ModelProperties.Should().BeEmpty();
      bundle.PropertySetDefinitions.Should().BeEmpty();
      bundle.Relations.PlacesByObject.Should().BeEmpty();
      bundle.Relations.MemberObjectsByDefinition.Should().BeEmpty();
      bundle.Relations.MaterialByObject.Should().BeEmpty();
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
