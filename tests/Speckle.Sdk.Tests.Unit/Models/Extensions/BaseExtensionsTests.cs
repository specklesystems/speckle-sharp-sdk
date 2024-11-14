using Shouldly;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;
using Speckle.Sdk.Models.Extensions;

namespace Speckle.Sdk.Tests.Unit.Models.Extensions;

public class BaseExtensionsTests
{
  [Before(Class)]
  public void Setup()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly);
  }

  [Test]
  [Arguments("myDynamicProp")]
  [Arguments("elements")]
  public void GetDetachedPropName_Dynamic(string propertyName)
  {
    var data = new TestBase();

    var result = data.GetDetachedPropName(propertyName);
    var expected = $"@{propertyName}";
    result.ShouldBe(expected);
  }

  [Test]
  [Arguments(nameof(TestBase.myProperty))]
  [Arguments(nameof(TestBase.myOtherProperty))]
  public void GetDetachedPropName_Instance(string propertyName)
  {
    var data = new TestBase();
    var result = data.GetDetachedPropName(propertyName);

    result.ShouldBe(propertyName);
  }

  [Test]
  public void TraverseWithPath()
  {
    var collection = new Collection() { name = "collection" };
    var subCollection = new Collection { name = "subCollection" };
    collection.elements.Add(subCollection);
    var data1 = new Base();
    subCollection.elements.Add(data1);

    var basePaths = collection.TraverseWithPath((obj => obj is not Collection)).ToList();

    basePaths.Count.ShouldBe(3);
    basePaths[0].Item2.speckle_type.ShouldBe("Speckle.Core.Models.Collections.Collection");
    basePaths[0].Item2["name"].ShouldBe("collection");
    basePaths[0].Item1.ShouldBe(new List<string>());

    basePaths[1].Item2.speckle_type.ShouldBe("Speckle.Core.Models.Collections.Collection");
    basePaths[1].Item2["name"].ShouldBe("subCollection");
    basePaths[1].Item1.ShouldBe(new List<string>() { "collection" });

    basePaths[2].Item2.speckle_type.ShouldBe("Base");
    basePaths[2].Item1.ShouldBe(new List<string>() { "collection", "subCollection" });
  }

  [SpeckleType("Speckle.Core.Tests.Unit.Models.Extensions.BaseExtensionsTests+TestBase")]
  public class TestBase : Base
  {
    public string myProperty { get; set; }
    public string myOtherProperty { get; set; }
  }
}
