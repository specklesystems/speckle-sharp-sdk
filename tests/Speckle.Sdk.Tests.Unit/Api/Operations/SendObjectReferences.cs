using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Speckle.Sdk.Api;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Tests.Unit.Api.Operations;

public class SendObjectReferences
{
  private IOperations _operations;

  public  SendObjectReferences()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(DataChunk).Assembly);
    var serviceProvider = TestServiceSetup.GetServiceProvider();
    _operations = serviceProvider.GetRequiredService<IOperations>();
  }

  [Test]
  [Arguments(0)]
  [Arguments(1)]
  [Arguments(10)]
  public async Task SendObjectsWithApplicationIds(int testDepth)
  {
    Base testData = GenerateTestCase(testDepth, true);
    MemoryTransport transport = new();
    var result = await _operations.Send(testData, [transport]);

    result.rootObjId.ShouldNotBeNull();
    result.rootObjId.Length.ShouldBe(32);

   result.convertedReferences.Count.ShouldBe((int)Math.Pow(2, testDepth + 1) - 2);
  }

  [Test]
  [Arguments(0)]
  [Arguments(1)]
  [Arguments(10)]
  public async Task SendObjectsWithoutApplicationIds(int testDepth)
  {
    Base testData = GenerateTestCase(testDepth, false);
    MemoryTransport transport = new();
    var result = await _operations.Send(testData, [transport]);

    result.rootObjId.ShouldNotBeNull();
    result.rootObjId.Length.ShouldBe(32);

    result.convertedReferences.ShouldBeEmpty();
  }

  private Base GenerateTestCase(int depth, bool withAppId)
  {
    var appId = withAppId ? $"{Guid.NewGuid()}" : null;
    var ret = new Base() { applicationId = appId };
    if (depth > 0)
    {
      ret["@elements"] = new List<Base>
      {
        GenerateTestCase(depth - 1, withAppId),
        GenerateTestCase(depth - 1, withAppId),
      };
    }

    return ret;
  }
}
