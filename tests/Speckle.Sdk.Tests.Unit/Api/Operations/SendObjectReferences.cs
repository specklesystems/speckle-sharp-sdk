using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk.Api;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Tests.Unit.Api.Operations;

public class SendObjectReferences
{
  private readonly IOperations _operations;

  public SendObjectReferences()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(DataChunk).Assembly);
    var serviceProvider = TestServiceSetup.GetServiceProvider();
    _operations = serviceProvider.GetRequiredService<IOperations>();
  }

  [Theory]
  [InlineData(0)]
  [InlineData(1)]
  [InlineData(10)]
  public async Task SendObjectsWithApplicationIds(int testDepth)
  {
    Base testData = GenerateTestCase(testDepth, true);
    MemoryTransport transport = new();
    var result = await _operations.Send(testData, [transport]);

    result.rootObjId.Should().NotBeNull();

    result.rootObjId.Length.Should().Be(32);

    result.convertedReferences.Count.Should().Be((int)(Math.Pow(2, testDepth + 1) - 2));
  }

  [Theory]
  [InlineData(0)]
  [InlineData(1)]
  [InlineData(10)]
  public async Task SendObjectsWithoutApplicationIds(int testDepth)
  {
    Base testData = GenerateTestCase(testDepth, false);
    MemoryTransport transport = new();
    var result = await _operations.Send(testData, [transport]);

    result.rootObjId.Should().NotBeNull();

    result.rootObjId.Length.Should().Be(32);

    result.convertedReferences.Should().BeEmpty();
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
