using NUnit.Framework;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;
using Speckle.Sdk.Models.Extensions;

namespace Speckle.Sdk.Tests.Unit.Models.Extensions;

[TestFixture]
[TestOf(nameof(BaseExtensions))]
public class BaseExtensionsTests
{
  [SetUp]
  public void Setup()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly);
  }

  [Test]
  [TestCase("myDynamicProp")]
  [TestCase("elements")]
  public void GetDetachedPropName_Dynamic(string propertyName)
  {
    var data = new TestBase();

    var result = data.GetDetachedPropName(propertyName);
    var expected = $"@{propertyName}";
    Assert.That(result, Is.EqualTo(expected));
  }

  [Test]
  [TestCase(nameof(TestBase.myProperty))]
  [TestCase(nameof(TestBase.myOtherProperty))]
  public void GetDetachedPropName_Instance(string propertyName)
  {
    var data = new TestBase();
    var result = data.GetDetachedPropName(propertyName);

    Assert.That(result, Is.EqualTo(propertyName));
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

    Assert.That(basePaths.Count, Is.EqualTo(3));
    Assert.That(basePaths[0].Item2.speckle_type, Is.EqualTo("Speckle.Core.Models.Collections.Collection"));
    Assert.That(basePaths[0].Item2["name"], Is.EqualTo("collection"));
    Assert.That(basePaths[0].Item1, Is.EqualTo(new List<string>()));

    Assert.That(basePaths[1].Item2.speckle_type, Is.EqualTo("Speckle.Core.Models.Collections.Collection"));
    Assert.That(basePaths[1].Item2["name"], Is.EqualTo("subCollection"));
    Assert.That(basePaths[1].Item1, Is.EqualTo(new List<string>() { "collection" }));

    Assert.That(basePaths[2].Item2.speckle_type, Is.EqualTo("Base"));
    Assert.That(basePaths[2].Item1, Is.EqualTo(new List<string>() { "collection", "subCollection" }));
  }

  [SpeckleType("Speckle.Core.Tests.Unit.Models.Extensions.BaseExtensionsTests+TestBase")]
  public class TestBase : Base
  {
    public string myProperty { get; set; }
    public string myOtherProperty { get; set; }
  }
}
