using System.Reflection;
using NUnit.Framework;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Tests.Unit.Host;

namespace Speckle.Sdk.Tests.Unit.Serialisation;

public class SimpleRoundTripTests
{
  static SimpleRoundTripTests()
  {
    Reset();
  }

  private static void Reset()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, Assembly.GetExecutingAssembly());
  }

  public static IEnumerable<Base> TestData()
  {
    yield return new DiningTable { ["@strangeVariable_NAme3"] = new TableLegFixture() };

    var polyline = new Polyline();
    for (int i = 0; i < 100; i++)
    {
      polyline.Points.Add(new Point { X = i * 2, Y = i % 2 });
    }
    yield return polyline;
  }

  [SetUp]
  public void Setup() => Reset();

  [TestCaseSource(nameof(TestData))]
  public async Task SimpleSerialization(Base testData)
  {
    var result = Sdk.Api.Operations.Serialize(testData);
    var test = await Sdk.Api.Operations.DeserializeAsync(result);

    Assert.That(testData.GetId(), Is.EqualTo(test.GetId()));
  }
}
