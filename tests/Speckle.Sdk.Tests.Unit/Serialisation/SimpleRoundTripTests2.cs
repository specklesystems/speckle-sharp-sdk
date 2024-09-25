using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Speckle.Sdk.Api;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Tests.Unit.Host;

namespace Speckle.Sdk.Tests.Unit.Serialisation;

public class SimpleRoundTripTests2
{
  private IOperations _operations;

  static SimpleRoundTripTests2()
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
  public void Setup()
  {
    Reset();

    var serviceProvider = TestServiceSetup.GetServiceProvider();
    _operations = serviceProvider.GetRequiredService<IOperations>();
  }

  [TestCaseSource(nameof(TestData))]
  public async Task SimpleSerialization(Base testData)
  {
    var result = _operations.Serialize2(testData);
    var test = await _operations.DeserializeAsync(result);

    Assert.That(testData.GetId(), Is.EqualTo(test.GetId()));
  }
}
