using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Speckle.Sdk.Api;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Tests.Unit.Api.Operations;

public class SendObjectReferences
{
  private IOperations _operations;

  [SetUp]
  public void Setup()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(DataChunk).Assembly);
    var serviceCollection = new ServiceCollection();
    serviceCollection.AddSpeckleSdk(new SpeckleConfiguration(HostApplications.Navisworks, HostAppVersion.v2023));
    var serviceProvider = serviceCollection.BuildServiceProvider();
    _operations = serviceProvider.GetRequiredService<IOperations>();
  }

  [TestCase(0)]
  [TestCase(1)]
  [TestCase(10)]
  public async Task SendObjectsWithApplicationIds(int testDepth)
  {
    Base testData = GenerateTestCase(testDepth, true);
    MemoryTransport transport = new();
    var result = await _operations.Send(testData, [transport]);

    Assert.That(result.rootObjId, Is.Not.Null);
    Assert.That(result.rootObjId, Has.Length.EqualTo(32));

    Assert.That(result.convertedReferences, Has.Count.EqualTo(Math.Pow(2, testDepth + 1) - 2));
  }

  [TestCase(0)]
  [TestCase(1)]
  [TestCase(10)]
  public async Task SendObjectsWithoutApplicationIds(int testDepth)
  {
    Base testData = GenerateTestCase(testDepth, false);
    MemoryTransport transport = new();
    var result = await _operations.Send(testData, [transport]);

    Assert.That(result.rootObjId, Is.Not.Null);
    Assert.That(result.rootObjId, Has.Length.EqualTo(32));

    Assert.That(result.convertedReferences, Is.Empty);
  }

  private Base GenerateTestCase(int depth, bool withAppId)
  {
    var appId = withAppId ? $"{Guid.NewGuid()}" : null;
    var ret = new Base() { applicationId = appId, };
    if (depth > 0)
    {
      ret["@elements"] = new List<Base>
      {
        GenerateTestCase(depth - 1, withAppId),
        GenerateTestCase(depth - 1, withAppId)
      };
    }

    return ret;
  }
}
