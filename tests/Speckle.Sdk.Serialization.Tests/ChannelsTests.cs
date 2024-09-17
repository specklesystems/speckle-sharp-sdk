using NUnit.Framework;
using Shouldly;
using Speckle.Newtonsoft.Json.Linq;
using Speckle.Objects.BuiltElements;
using Speckle.Sdk.Common;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.Receive;
using Speckle.Sdk.Serialisation.Send;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialization.Tests;

public class ChannelsTests
{
  [SetUp]
  public void Setup()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(Wall).Assembly);
  }

  [Test]
  [TestCase("RevitObject.json")]
  public async Task Deserialize_Serialize_Channel_Test(string fileName)
  {
    var objects = await TestHelper.ReadAsObjectsFromResource(fileName);
    var bases = new Dictionary<string, (string, Base)>();
    foreach (var (id, objJson) in objects)
    {
      var jObject = JObject.Parse(objJson);
      var oldSpeckleType = jObject["speckle_type"].NotNull().Value<string>().NotNull();
      var starts = oldSpeckleType.StartsWith("Speckle.Core.") || oldSpeckleType.StartsWith("Objects.");
      starts.ShouldBeTrue($"{oldSpeckleType} isn't expected");

      using var stage = new ReceiveProcess(new MemorySource(objects));
      var baseType = await stage.GetObject(id, _ => { }, default).ConfigureAwait(false);
      baseType.id.ShouldBe(id);
      bases.Add(baseType.id, (objJson, baseType));

      starts = baseType.speckle_type.StartsWith("Speckle.Core.") || baseType.speckle_type.StartsWith("Objects.");
      starts.ShouldBeTrue($"{baseType.speckle_type} isn't expected");

      var type = TypeLoader.GetAtomicType(baseType.speckle_type);
      type.ShouldNotBeNull();
      var name = TypeLoader.GetTypeString(type) ?? throw new ArgumentNullException();
      starts = name.StartsWith("Speckle.Core") || name.StartsWith("Objects");
      starts.ShouldBeTrue($"{name} isn't expected");
    }

    var target = new MemoryTarget();
    foreach (var (id, (objJson, baseObj)) in bases)
    {
      using var stage = new SendProcess(target);
      var (objId, references) = await stage.SaveObject(baseObj, _ => { }, default).ConfigureAwait(false);
      var orig = JObject.Parse(target.Sent[objId]);
      var targ = JObject.Parse(objJson);
      JToken.DeepEquals(orig, targ);
      //      objId.ShouldBe(id);
    }
  }

  [Test]
  [TestCase("7238c0e0bbd1bafbe1a287aa7fc88619.json.gz")]
  public async Task Channels_Deserialize(string fileName)
  {
    var closures = await TestHelper.ReadAsObjectsFromResource(fileName);

    using var process = new ReceiveProcess(
      new MemorySource(closures),
      new ReceiveProcessSettings(1, 1, DeserializedOptions: new DeserializedOptions(SkipInvalidConverts: true))
    );
    var root = await process.GetObject("7238c0e0bbd1bafbe1a287aa7fc88619", _ => { }, default);
    root.id.ShouldBe("7238c0e0bbd1bafbe1a287aa7fc88619");
  }
}
