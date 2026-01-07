using AwesomeAssertions;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;
using Speckle.Sdk.Models.Extensions;

namespace Speckle.Sdk.Tests.Unit.Models.Extensions;

public class BaseExtensionsTests
{
  public BaseExtensionsTests()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(TestBase).Assembly);
  }

  [Theory]
  [InlineData("myDynamicProp")]
  [InlineData("elements")]
  public void GetDetachedPropName_Dynamic(string propertyName)
  {
    var data = new TestBase();

    var result = data.GetDetachedPropName(propertyName);
    var expected = $"@{propertyName}";
    result.Should().Be(expected);
  }

  [Theory]
  [InlineData(nameof(TestBase.myProperty))]
  [InlineData(nameof(TestBase.myOtherProperty))]
  public void GetDetachedPropName_Instance(string propertyName)
  {
    var data = new TestBase();
    var result = data.GetDetachedPropName(propertyName);

    result.Should().Be(propertyName);
  }

  [Fact]
  public void TraverseWithPath()
  {
    var collection = new Collection("collection");
    var subCollection = new Collection("subCollection");
    collection.elements.Add(subCollection);
    var data1 = new Base();
    subCollection.elements.Add(data1);

    var basePaths = collection.TraverseWithPath((obj => obj is not Collection)).ToList();

    basePaths.Count.Should().Be(3);

    basePaths[0].Item2.speckle_type.Should().Be("Speckle.Core.Models.Collections.Collection");
    basePaths[0].Item2["name"].Should().Be("collection");
    basePaths[0].Item1.Should().BeEquivalentTo(new List<string>());

    basePaths[1].Item2.speckle_type.Should().Be("Speckle.Core.Models.Collections.Collection");
    basePaths[1].Item2["name"].Should().Be("subCollection");
    basePaths[1].Item1.Should().BeEquivalentTo(new List<string>() { "collection" });

    basePaths[2].Item2.speckle_type.Should().Be("Base");
    basePaths[2].Item1.Should().BeEquivalentTo(new List<string>() { "collection", "subCollection" });
  }

  [SpeckleType("Speckle.Core.Tests.Unit.Models.Extensions.BaseExtensionsTests+TestBase")]
  public class TestBase : Base
  {
    public string myProperty { get; set; }
    public string myOtherProperty { get; set; }
  }
}
