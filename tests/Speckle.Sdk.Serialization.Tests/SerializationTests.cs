using System.Reflection;
using NUnit.Framework;
using Shouldly;
using Speckle.Newtonsoft.Json.Linq;
using Speckle.Objects.BuiltElements;
using Speckle.Sdk.Common;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Serialisation.Utilities;
using Speckle.Sdk.Serialisation.V2.Receive;

namespace Speckle.Sdk.Serialization.Tests;

[TestFixture]
[Description("For certain types, changing property from one type to another should be implicitly backwards compatible")]
public class SerializationTests
{
  private class TestLoader(string json) : IObjectLoader
  {
    public Task<(string, IReadOnlyList<string>)> GetAndCache(
      string rootId,
      CancellationToken cancellationToken,
      DeserializeOptions? options = null
    )
    {
      var childrenIds = ClosureParser.GetChildrenIds(json).ToList();
      return Task.FromResult<(string, IReadOnlyList<string>)>((json, childrenIds));
    }

    public string? LoadId(string id) => null;
  }

  private readonly Assembly _assembly = Assembly.GetExecutingAssembly();

  [SetUp]
  public void Setup()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(Wall).Assembly, _assembly);
  }

  private async Task<string> ReadJson(string fullName)
  {
    await using var stream = _assembly.GetManifestResourceStream(fullName).NotNull();
    using var reader = new StreamReader(stream);
    return await reader.ReadToEndAsync();
  }

  private Dictionary<string, string> ReadAsObjects(string json)
  {
    var jsonObjects = new Dictionary<string, string>();
    var array = JArray.Parse(json);
    foreach (var obj in array)
    {
      if (obj is JObject jobj)
      {
        jsonObjects.Add(jobj["id"].NotNull().Value<string>().NotNull(), jobj.ToString());
      }
    }
    return jsonObjects;
  }

  /*
    [Test]
    [TestCase("RevitObject.json")]
    public async Task RunTest2(string fileName)
    {
      var fullName = _assembly.GetManifestResourceNames().Single(x => x.EndsWith(fileName));
      var json = await ReadJson(fullName);
      var closure = await ReadAsObjects(json);
      using DeserializeProcess sut = new(null, new TestLoader(json), new TestTransport(closure));
      var @base = await sut.Deserialize("551513ff4f3596024547fc818f1f3f70");
      @base.ShouldNotBeNull();
    }*/

  [Test]
  [TestCase("RevitObject.json")]
  public async Task Basic_Namespace_Validation(string fileName)
  {
    var fullName = _assembly.GetManifestResourceNames().Single(x => x.EndsWith(fileName));
    var json = await ReadJson(fullName);
    var closure = ReadAsObjects(json);
    var deserializer = new SpeckleObjectDeserializer
    {
      ReadTransport = new TestTransport(closure),
      CancellationToken = default,
    };

    foreach (var (id, objJson) in closure)
    {
      var jObject = JObject.Parse(objJson);
      var oldSpeckleType = jObject["speckle_type"].NotNull().Value<string>().NotNull();
      var starts = oldSpeckleType.StartsWith("Speckle.Core.") || oldSpeckleType.StartsWith("Objects.");
      starts.ShouldBeTrue($"{oldSpeckleType} isn't expected");

      var baseType = await deserializer.DeserializeAsync(objJson);
      baseType.id.ShouldBe(id);

      starts = baseType.speckle_type.StartsWith("Speckle.Core.") || baseType.speckle_type.StartsWith("Objects.");
      starts.ShouldBeTrue($"{baseType.speckle_type} isn't expected");

      var type = TypeLoader.GetAtomicType(baseType.speckle_type);
      type.ShouldNotBeNull();
      var name = TypeLoader.GetTypeString(type) ?? throw new ArgumentNullException();
      starts = name.StartsWith("Speckle.Core") || name.StartsWith("Objects");
      starts.ShouldBeTrue($"{name} isn't expected");
    }
  }

  [Test]
  [TestCase("RevitObject.json")]
  public async Task Basic_Namespace_Validation_New(string fileName)
  {
    var fullName = _assembly.GetManifestResourceNames().Single(x => x.EndsWith(fileName));
    var json = await ReadJson(fullName);
    var closures = ReadAsObjects(json);
    var process = new DeserializeProcess(null, new TestObjectLoader(closures));
    await process.Deserialize("551513ff4f3596024547fc818f1f3f70", default);
    foreach (var (id, objJson) in closures)
    {
      var jObject = JObject.Parse(objJson);
      var oldSpeckleType = jObject["speckle_type"].NotNull().Value<string>().NotNull();
      var starts = oldSpeckleType.StartsWith("Speckle.Core.") || oldSpeckleType.StartsWith("Objects.");
      starts.ShouldBeTrue($"{oldSpeckleType} isn't expected");

      var baseType = process.BaseCache[id];

      starts = baseType.speckle_type.StartsWith("Speckle.Core.") || baseType.speckle_type.StartsWith("Objects.");
      starts.ShouldBeTrue($"{baseType.speckle_type} isn't expected");

      var type = TypeLoader.GetAtomicType(baseType.speckle_type);
      type.ShouldNotBeNull();
      var name = TypeLoader.GetTypeString(type) ?? throw new ArgumentNullException();
      starts = name.StartsWith("Speckle.Core") || name.StartsWith("Objects");
      starts.ShouldBeTrue($"{name} isn't expected");
    }
  }
}
