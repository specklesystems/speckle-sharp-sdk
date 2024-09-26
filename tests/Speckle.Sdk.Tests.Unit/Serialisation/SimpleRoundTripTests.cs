using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Speckle.Sdk.Api;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Tests.Unit.Host;

namespace Speckle.Sdk.Tests.Unit.Serialisation;

public class SimpleRoundTripTests
{
  private IOperations _operations;

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
  public void Setup()
  {
    Reset();

    var serviceProvider = TestServiceSetup.GetServiceProvider();
    _operations = serviceProvider.GetRequiredService<IOperations>();
  }

  [TestCaseSource(nameof(TestData))]
  public async Task SimpleSerialization(Base testData)
  {
    var result = await _operations.Serialize(testData);
    var test = await _operations.DeserializeAsync(result);

    Assert.That(await testData.GetIdAsync(), Is.EqualTo(await test.GetIdAsync()));
  }
}
