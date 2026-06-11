using AwesomeAssertions;
using Speckle.Newtonsoft.Json.Linq;
using Speckle.Sdk.Pipelines.Send.Artifacts;

namespace Speckle.Sdk.Tests.Unit.Pipelines.Send.Artifacts;

public class EavExtractionTests
{
  [Fact]
  public void DataObject_ExtractsRootScalars_PropertiesWalk_Location_DisplayValue_Elements()
  {
    var obj = JObject.Parse(
      """
      {
        "speckle_type": "Objects.Data.DataObject:Objects.Data.NavisworksObject",
        "name": "Bolt M12",
        "category": "Fasteners",
        "applicationId": "app-1",
        "units": "m",
        "properties": {
          "Item": {
            "Material": { "name": "Material", "value": "Steel", "units": null },
            "Length": { "name": "Length", "value": 0.12, "units": "m", "internalDefinitionName": "LEN" }
          },
          "plainString": "hello",
          "plainNumber": 42
        },
        "location": { "x": 1.5, "y": -2.0, "z": 0.0, "units": "m" },
        "displayValue": [
          { "speckle_type": "reference", "referencedId": "geomhash1" },
          { "speckle_type": "Objects.Geometry.Mesh", "id": "geomhash2" }
        ],
        "elements": [
          { "speckle_type": "reference", "referencedId": "child1" },
          { "id": "child2" }
        ]
      }
      """
    );

    var rows = EavExtraction.FlattenObjectProperties("obj1", obj);

    // Root scalars
    rows.Should().ContainSingle(r => r.Path == "name" && r.ValueText == "Bolt M12" && r.Type == "string");
    rows.Should().ContainSingle(r => r.Path == "category" && r.ValueText == "Fasteners");
    rows.Should().ContainSingle(r => r.Path == "applicationId" && r.ValueText == "app-1");

    // Parameter pattern collapsed to its value, units + idn carried
    rows.Should().ContainSingle(r => r.Path == "properties.Item.Material" && r.ValueText == "Steel");
    var len = rows.Single(r => r.Path == "properties.Item.Length");
    len.ValueNum.Should().Be(0.12);
    len.Type.Should().Be("number");
    len.Units.Should().Be("m");
    len.InternalDefinitionName.Should().Be("LEN");

    // Plain leaves
    rows.Should().ContainSingle(r => r.Path == "properties.plainString" && r.ValueText == "hello");
    rows.Should().ContainSingle(r => r.Path == "properties.plainNumber" && r.ValueNum == 42);

    // Location
    rows.Should().ContainSingle(r => r.Path == "location.x" && r.ValueNum == 1.5 && r.Units == "m");
    rows.Should().ContainSingle(r => r.Path == "location.y" && r.ValueNum == -2.0);
    rows.Should().ContainSingle(r => r.Path == "location.z" && r.ValueNum == 0.0);

    // DisplayValue refs: referencedId for references, id for inlined
    rows.Should().ContainSingle(r => r.Path == "displayValue.0.referencedId" && r.ValueText == "geomhash1");
    rows.Should().ContainSingle(r => r.Path == "displayValue.1.referencedId" && r.ValueText == "geomhash2");

    // Elements as one JSON array row
    rows.Should().ContainSingle(r => r.Path == "elements" && r.ValueText == """["child1","child2"]""");
  }

  [Fact]
  public void InstanceProxy_ExtractsTransformComponents_AndFullMatrix()
  {
    var obj = JObject.Parse(
      """
      {
        "speckle_type": "Speckle.Core.Models.Instances.InstanceProxy",
        "definitionId": "def-1",
        "units": "ft",
        "transform": [1,0,0,10.5, 0,1,0,20.25, 0,0,1,-3.0, 0,0,0,1]
      }
      """
    );

    var rows = EavExtraction.FlattenObjectProperties("ip1", obj);

    rows.Should().ContainSingle(r => r.Path == "definitionId" && r.ValueText == "def-1");
    rows.Should().ContainSingle(r => r.Path == "proxy.transform.tx" && r.ValueNum == 10.5 && r.Units == "ft");
    rows.Should().ContainSingle(r => r.Path == "proxy.transform.ty" && r.ValueNum == 20.25);
    rows.Should().ContainSingle(r => r.Path == "proxy.transform.tz" && r.ValueNum == -3.0);

    var matrix = rows.Single(r => r.Path == "proxy.transform.matrix");
    matrix.ValueText.Should().StartWith("[1");
    matrix.ValueText.Should().Contain("10.5");
  }

  [Fact]
  public void Collection_ExtractsNameAndElements()
  {
    var obj = JObject.Parse(
      """
      {
        "speckle_type": "Speckle.Core.Models.Collections.Collection",
        "name": "Level 1",
        "elements": [ { "referencedId": "a" }, { "referencedId": "b" } ]
      }
      """
    );

    var rows = EavExtraction.FlattenObjectProperties("col1", obj);

    rows.Should().ContainSingle(r => r.Path == "name" && r.ValueText == "Level 1");
    rows.Should().ContainSingle(r => r.Path == "elements" && r.ValueText == """["a","b"]""");
  }

  [Fact]
  public void GeometryTypes_ProduceNoRows()
  {
    var mesh = JObject.Parse("""{ "speckle_type": "Objects.Geometry.Mesh", "units": "m" }""");
    EavExtraction.FlattenObjectProperties("m1", mesh).Should().BeEmpty();

    EavExtraction.ProducesRows("Objects.Geometry.Mesh").Should().BeFalse();
    EavExtraction.ProducesRows("Objects.Geometry.MeshBinary").Should().BeFalse();
    EavExtraction.ProducesRows("Speckle.Core.Models.Blob").Should().BeFalse();
    EavExtraction.ProducesRows("Speckle.Core.Models.DataChunk").Should().BeFalse();

    EavExtraction.ProducesRows("Speckle.Core.Models.Instances.InstanceProxy").Should().BeTrue();
    EavExtraction.ProducesRows("Objects.Data.DataObject:Objects.Data.NavisworksObject").Should().BeTrue();
    EavExtraction.ProducesRows("Speckle.Core.Models.Collections.Collection").Should().BeTrue();
    // InstanceDefinitionProxy contains "Proxy" but NOT the contiguous "InstanceProxy";
    // it matches no branch and must produce no rows (parity with the TS dispatch).
    EavExtraction.ProducesRows("Speckle.Core.Models.Instances.InstanceDefinitionProxy").Should().BeFalse();
  }

  [Theory]
  [InlineData("\"true\"", "boolean", null)]
  [InlineData("\"FALSE\"", "boolean", null)]
  [InlineData("\"42.5\"", "number", 42.5)]
  [InlineData("\"  7  \"", "number", 7.0)]
  [InlineData("\"e89b00ed-5ca8-46a2\"", "string", null)] // UUID-like rejected from numeric inference
  [InlineData("\"\"", "string", null)]
  [InlineData("\"hello\"", "string", null)]
  [InlineData("true", "boolean", null)]
  [InlineData("3.25", "number", 3.25)]
  public void TypeInference_MatchesServerBehaviour(string jsonValue, string expectedType, double? expectedNum)
  {
    var obj = JObject.Parse($$"""{ "speckle_type": "Objects.Data.DataObject", "properties": { "p": {{jsonValue}} } }""");

    var rows = EavExtraction.FlattenObjectProperties("o", obj);
    var row = rows.Single(r => r.Path == "properties.p");

    row.Type.Should().Be(expectedType);
    row.ValueNum.Should().Be(expectedNum);
  }

  [Fact]
  public void BooleanValueText_IsLowercase_LikeJavascript()
  {
    var obj = JObject.Parse("""{ "speckle_type": "Objects.Data.DataObject", "properties": { "flag": true } }""");
    var row = EavExtraction.FlattenObjectProperties("o", obj).Single(r => r.Path == "properties.flag");
    row.ValueText.Should().Be("true");
  }

  [Fact]
  public void StructureUnderTypeParameters_AndMaterialQuantities_AreSkippedInWalk()
  {
    var obj = JObject.Parse(
      """
      {
        "speckle_type": "Objects.Data.DataObject",
        "properties": {
          "Type Parameters": {
            "Structure": { "layerCount": 3 }
          },
          "Material Quantities": {
            "Concrete": {
              "materialCategory": "Structural",
              "area": { "value": 12.5, "units": "m2" },
              "volume": { "value": 2.25, "units": "m3" }
            }
          }
        }
      }
      """
    );

    var rows = EavExtraction.FlattenObjectProperties("o", obj);

    // Structure skipped in walk
    rows.Should().NotContain(r => r.Path.Contains("Structure"));

    // Material Quantities handled via the special extraction with category in path
    rows.Should()
      .ContainSingle(r => r.Path == "properties.Material Quantities.Structural.Concrete.area" && r.ValueNum == 12.5 && r.Units == "m2");
    rows.Should()
      .ContainSingle(r => r.Path == "properties.Material Quantities.Structural.Concrete.volume" && r.ValueNum == 2.25 && r.Units == "m3");
  }

  [Fact]
  public void DepthLimit_StopsAtTen()
  {
    // Build properties nested 12 levels deep
    var inner = new JObject { ["leaf"] = "deep" };
    for (var i = 0; i < 12; i++)
    {
      inner = new JObject { [$"l{i}"] = inner };
    }
    var obj = new JObject { ["speckle_type"] = "Objects.Data.DataObject", ["properties"] = inner };

    var rows = EavExtraction.FlattenObjectProperties("o", obj);

    rows.Should().NotContain(r => r.Path.EndsWith(".leaf"));
  }
}
