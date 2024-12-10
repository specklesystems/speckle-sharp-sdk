using NUnit.Framework;
using Shouldly;
using Speckle.Newtonsoft.Json.Linq;
using Speckle.Objects.Geometry;
using Speckle.Objects.Primitive;
using Speckle.Sdk.Common;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Extensions;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Serialisation.V2.Send;

namespace Speckle.Sdk.Serialization.Tests;

public class ExternalIdTests
{
  [SetUp]
  public void Setup()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(Polyline).Assembly);
  }

  [Test]
  [TestCase("cfaf7ae0dfc5a7cf3343bb6db46ed238", "8d27f5c7fac36d985d89bb6d6d8acddc")]
  public void ExternalIdTest_Detached(string lineId, string valueId)
  {
    var p = new Polyline() { units = "cm", value = [1, 2] };
    var serializer = new ObjectSerializer(new BasePropertyGatherer(), new Dictionary<Base, CacheInfo>(), true);
    var list = serializer.Serialize(p).ToDictionary(x => x.Item1, x => x.Item2);
    list.ContainsKey(new Id(lineId)).ShouldBeTrue();
    var json = list[new Id(lineId)];
    var jObject = JObject.Parse(json.Value);
    jObject.ContainsKey("__closure").ShouldBeTrue();
    var closures = (JObject)jObject["__closure"].NotNull();
    closures.ContainsKey(valueId).ShouldBeTrue();
  }

  [Test]
  [TestCase("cfaf7ae0dfc5a7cf3343bb6db46ed238", "8d27f5c7fac36d985d89bb6d6d8acddc")]
  public void ExternalIdTest_Detached_Nested(string lineId, string valueId)
  {
    var curve = new Curve()
    {
      closed = false,
      displayValue = new Polyline() { units = "cm", value = [1, 2] },
      domain = new Interval() { start = 0, end = 1 },
      units = "cm",
      degree = 1,
      periodic = false,
      rational = false,
      points = [],
      knots = [],
      weights = [],
    };
    var serializer = new ObjectSerializer(new BasePropertyGatherer(), new Dictionary<Base, CacheInfo>(), true);
    var list = serializer.Serialize(curve).ToDictionary(x => x.Item1, x => x.Item2);
    list.ContainsKey(new Id(lineId)).ShouldBeTrue();
    var json = list[new Id(lineId)];
    var jObject = JObject.Parse(json.Value);
    jObject.ContainsKey("__closure").ShouldBeTrue();
    var closures = (JObject)jObject["__closure"].NotNull();
    closures.ContainsKey(valueId).ShouldBeTrue();
  }

  [Test]
  [TestCase("cfaf7ae0dfc5a7cf3343bb6db46ed238", "8d27f5c7fac36d985d89bb6d6d8acddc")]
  public void ExternalIdTest_Detached_Nested_More(string lineId, string valueId)
  {
    var curve = new Curve()
    {
      closed = false,
      displayValue = new Polyline() { units = "cm", value = [1, 2] },
      domain = new Interval() { start = 0, end = 1 },
      units = "cm",
      degree = 1,
      periodic = false,
      rational = false,
      points = [],
      knots = [],
      weights = [],
    };
    var polycurve = new Polycurve() { segments = [curve], units = "cm" };
    var serializer = new ObjectSerializer(new BasePropertyGatherer(), new Dictionary<Base, CacheInfo>(), true);
    var list = serializer.Serialize(polycurve).ToDictionary(x => x.Item1, x => x.Item2);
    list.ContainsKey(new Id(lineId)).ShouldBeTrue();
    var json = list[new Id(lineId)];
    var jObject = JObject.Parse(json.Value);
    jObject.ContainsKey("__closure").ShouldBeTrue();
    var closures = (JObject)jObject["__closure"].NotNull();
    closures.ContainsKey(valueId).ShouldBeTrue();
  }

  [Test]
  [TestCase("cfaf7ae0dfc5a7cf3343bb6db46ed238", "8d27f5c7fac36d985d89bb6d6d8acddc")]
  public void ExternalIdTest_Detached_Nested_More_Too(string lineId, string valueId)
  {
    var curve = new Curve()
    {
      closed = false,
      displayValue = new Polyline() { units = "cm", value = [1, 2] },
      domain = new Interval() { start = 0, end = 1 },
      units = "cm",
      degree = 1,
      periodic = false,
      rational = false,
      points = [],
      knots = [],
      weights = [],
    };
    var polycurve = new Polycurve() { segments = [curve], units = "cm" };
    var @base = new Base();
    @base.SetDetachedProp("profile", polycurve);
    var serializer = new ObjectSerializer(new BasePropertyGatherer(), new Dictionary<Base, CacheInfo>(), true);
    var list = serializer.Serialize(@base).ToDictionary(x => x.Item1, x => x.Item2);
    list.ContainsKey(new Id(lineId)).ShouldBeTrue();
    var json = list[new Id(lineId)];
    var jObject = JObject.Parse(json.Value);
    jObject.ContainsKey("__closure").ShouldBeTrue();
    var closures = (JObject)jObject["__closure"].NotNull();
    closures.ContainsKey(valueId).ShouldBeTrue();
  }
}
