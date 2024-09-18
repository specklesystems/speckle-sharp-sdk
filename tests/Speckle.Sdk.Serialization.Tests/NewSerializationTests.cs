using NUnit.Framework;
using Shouldly;
using Speckle.Newtonsoft.Json.Linq;
using Speckle.Objects.BuiltElements;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Serialisation.Receive;
using Speckle.Sdk.Serialisation.Send;

namespace Speckle.Sdk.Serialization.Tests;

[TestFixture]
[Description("For certain types, changing property from one type to another should be implicitly backwards compatible")]
public class NewSerializationTests
{
  [SetUp]
  public void Setup()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(Wall).Assembly);
  }

  [Test]
  [TestCase("RevitObjectsTop.json")]
  public async Task Deserialize_Both_Serialize_Both_Compare(string fileName)
  {
    var json = await TestHelper.ReadJsonFromResource(fileName);

    var oldDictionary = new Dictionary<string, string>();
    var oldDeserializer = new SpeckleObjectDeserializer
    {
      ReadTransport = new TestTransport(oldDictionary),
      CancellationToken = default
    };
    var oldBase = await oldDeserializer.DeserializeAsync(json);
    oldBase.id.ShouldBe("7238c0e0bbd1bafbe1a287aa7fc88619");
    var oldCollection = oldBase as Collection;
    oldCollection.ShouldNotBeNull();
    oldCollection.elements.Count.ShouldBe(78);

    var newDictionary = new Dictionary<string, Base>();
    SpeckleObjectDeserializer2 newDeserializer = new(newDictionary, SpeckleObjectSerializer2Pool.Instance, new(false));
    var newBase = newDeserializer.Deserialize(json);
    newBase.id.ShouldBe("7238c0e0bbd1bafbe1a287aa7fc88619");
    var newCollection = newBase as Collection;
    newCollection.ShouldNotBeNull();
    newCollection.elements.Count.ShouldBe(78);

    SpeckleObjectSerializer2 newSerializer = new(SpeckleObjectSerializer2Pool.Instance);
    var newJson = newSerializer.Serialize(newBase);

    SpeckleObjectSerializer oldSerializer = new();
    var oldJson = oldSerializer.Serialize(oldBase);
    newJson.Length.ShouldBe(oldJson.Length);

    JToken.DeepEquals(JObject.Parse(newJson), JObject.Parse(oldJson)).ShouldBeTrue();
  }
}
