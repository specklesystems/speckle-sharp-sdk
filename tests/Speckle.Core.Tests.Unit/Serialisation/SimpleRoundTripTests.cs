using NUnit.Framework;
using Speckle.Core.Models;
using Speckle.Core.Tests.Unit.Kits;

namespace Speckle.Core.Tests.Unit.Serialisation;

public class SimpleRoundTripTests
{
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

  [TestCaseSource(nameof(TestData))]
  public void SimpleSerialization(Base testData)
  {
    var result = Core.Api.Operations.Serialize(testData);
    var test = Core.Api.Operations.Deserialize(result);

    Assert.That(testData.GetId(), Is.EqualTo(test.GetId()));
  }
}
