using Speckle.Objects.Geometry;
using Speckle.Objects.Primitive;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Extensions;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Serialisation.V2.Send;

namespace Speckle.Sdk.Serialization.Tests;

public class ExternalIdTests
{
  public ExternalIdTests()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(Polyline).Assembly);
  }

  [Fact]
  public async Task ExternalIdTest_Detached()
  {
    var p = new Polyline() { units = "cm", value = [1, 2] };
    using var serializer = new ObjectSerializerFactory(new BasePropertyGatherer()).Create(
      new Dictionary<Id, NodeInfo>(),
      default
    );
    var objects = serializer.Serialize(p).ToDictionary(x => x.Item1, x => x.Item2);

    await VerifyJsonDictionary(objects);
  }

  [Fact]
  public async Task ExternalIdTest_Detached_Nested()
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
    using var serializer = new ObjectSerializerFactory(new BasePropertyGatherer()).Create(
      new Dictionary<Id, NodeInfo>(),
      default
    );
    var objects = serializer.Serialize(curve).ToDictionary(x => x.Item1, x => x.Item2);

    await VerifyJsonDictionary(objects);
  }

  [Fact]
  public  async Task ExternalIdTest_Detached_Nested_More()
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
    using var serializer = new ObjectSerializerFactory(new BasePropertyGatherer()).Create(
      new Dictionary<Id, NodeInfo>(),
      default
    );
    var objects = serializer.Serialize(polycurve).ToDictionary(x => x.Item1, x => x.Item2);

    await VerifyJsonDictionary(objects);
  }

  [Fact]
  public  async Task ExternalIdTest_Detached_Nested_More_Too()
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
    using var serializer = new ObjectSerializerFactory(new BasePropertyGatherer()).Create(
      new Dictionary<Id, NodeInfo>(),
      default
    );
    var objects = serializer.Serialize(@base).ToDictionary(x => x.Item1, x => x.Item2);
    await VerifyJsonDictionary(objects);
  }
}
