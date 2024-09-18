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
  [SetUp]
  public void Setup()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(Wall).Assembly);
  }

  [Test]
  [TestCase("RevitObject.json")]
  [TestCase("7238c0e0bbd1bafbe1a287aa7fc88619.json.gz")]
  public async Task Basic_Namespace_Validation(string fileName)
  {
    var closures = await TestHelper.ReadAsObjectsFromResource(fileName);
    var deserializer = new SpeckleObjectDeserializer
    {
      ReadTransport = new TestTransport(closures),
      CancellationToken = default,
      SkipInvalidConverts = true
    };
    foreach (var (id, objJson) in closures)
    {
      var jObject = JObject.Parse(objJson);
      var oldSpeckleType = jObject["speckle_type"].NotNull().Value<string>().NotNull();
      var starts =
        oldSpeckleType.StartsWith("Speckle.Core.")
        || oldSpeckleType.StartsWith("Objects.")
        || oldSpeckleType.StartsWith("Base");
      starts.ShouldBeTrue($"{oldSpeckleType} isn't expected");

      var baseType = await deserializer.DeserializeAsync(objJson);
      baseType.id.ShouldBe(id);

      starts =
        baseType.speckle_type.StartsWith("Speckle.Core.")
        || baseType.speckle_type.StartsWith("Objects.")
        || oldSpeckleType.StartsWith("Base");
      starts.ShouldBeTrue($"{baseType.speckle_type} isn't expected");

      var type = TypeLoader.GetAtomicType(baseType.speckle_type);
      type.ShouldNotBeNull();
      if (!type.FullName.NotNull().EndsWith("Base"))
      {
        var name = TypeLoader.GetTypeString(type) ?? throw new ArgumentNullException(type.FullName);
        starts = name.StartsWith("Speckle.Core") || name.StartsWith("Objects");
        starts.ShouldBeTrue($"{name} isn't expected");
      }
    }
  }
}
