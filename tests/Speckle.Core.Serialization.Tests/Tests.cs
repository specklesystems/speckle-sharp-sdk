using System.Reflection;
using FluentAssertions;
using NUnit.Framework;
using Speckle.Core.Common;
using Speckle.Core.Serialisation;
using Speckle.Core.Serialisation.SerializationUtilities;
using Speckle.Newtonsoft.Json.Linq;

namespace Speckle.Core.Serialization.Tests;

[TestFixture]
[Description("For certain types, changing property from one type to another should be implicitly backwards compatible")]
public class SerializationTests
{
  private readonly Assembly _assembly = Assembly.GetExecutingAssembly();
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
    var deserializer = new BaseObjectDeserializerV2
    {
      ReadTransport = new TestTransport(closure),
      CancellationToken = default
    };
    foreach(var (id, objJson) in closure)
    {
      var jObject = JObject.Parse(objJson);
      var oldSpeckleType = jObject["speckle_type"].NotNull().Value<string>().NotNull();
      var starts = oldSpeckleType.StartsWith("Speckle.Core.") ||
                   oldSpeckleType.StartsWith("Objects.");
      starts.Should().BeTrue($"{oldSpeckleType} isn't expected");
      
      var baseType = deserializer.Deserialize(objJson);
      id.Should().Be(baseType.id);
      
      starts = baseType.speckle_type.StartsWith("Speckle.Core.") ||
        baseType.speckle_type.StartsWith("Speckle.Objects.");
      starts.Should().BeTrue($"{baseType.speckle_type} isn't expected");
      
      var type = BaseObjectSerializationUtilities.GetAtomicType(baseType.speckle_type);
      type.Should().NotBeNull();
      var name = type.FullName.NotNull();
      starts =name.StartsWith("Speckle.Core") || name.StartsWith("Speckle.Objects");
      starts.Should().BeTrue($"{name} isn't expected");
    }
  }
}
