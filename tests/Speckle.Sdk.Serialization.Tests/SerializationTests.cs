using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Shouldly;
using Speckle.Newtonsoft.Json.Linq;
using Speckle.Objects.BuiltElements;
using Speckle.Sdk.Common;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;

namespace Speckle.Sdk.Serialization.Tests;

[TestFixture]
[Description("For certain types, changing property from one type to another should be implicitly backwards compatible")]
public class SerializationTests
{
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

  private async Task<Dictionary<string, string>> ReadAsObjects(string fullName)
  {
    var jsonObjects = new Dictionary<string, string>();
    var json = await ReadJson(fullName);
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

  [Test]
  [TestCase("RevitObject.json")]
  public async Task Basic_Namespace_Validation(string fileName)
  {
    var fullName = _assembly.GetManifestResourceNames().Single(x => x.EndsWith(fileName));
    var closure = await ReadAsObjects(fullName);
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
}
