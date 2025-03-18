using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk.Api;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Tests.Unit.Api.Operations;

public sealed partial class OperationsReceiveTests : IDisposable
{
  private static readonly Base[] s_testObjects;
  private readonly IOperations _operations;
  private readonly MemoryTransport _testCaseTransport;

  static OperationsReceiveTests()
  {
    s_testObjects =
    [
      new() { ["string prop"] = "simple test case", ["numerical prop"] = 123 },
      new() { ["@detachedProp"] = new Base() { ["the best prop"] = "1234!" } },
      new()
      {
        ["@detachedList"] = new List<Base> { new() { ["the worst prop"] = null } },
        ["dictionaryProp"] = new Dictionary<string, Base> { ["dict"] = new() { ["the best prop"] = "" } },
      },
    ];
  }

  public OperationsReceiveTests()
  {
    var serviceProvider = TestServiceSetup.GetServiceProvider(typeof(Base).Assembly, Assembly.GetExecutingAssembly());
    _operations = serviceProvider.GetRequiredService<IOperations>();
    _testCaseTransport = new MemoryTransport();

    // Simulate a one-time setup action
    foreach (var b in s_testObjects)
    {
      _ = _operations.Send(b, _testCaseTransport, false).GetAwaiter().GetResult();
    }
  }

  public static IEnumerable<object[]> TestCases()
  {
    foreach (var s in s_testObjects)
    {
      yield return [s.GetId(true)];
    }
  }

  [Theory]
  [MemberData(nameof(TestCases))]
  public async Task Receive_FromLocal_ExistingObjects(string id)
  {
    Base result = await _operations.Receive(id, null, _testCaseTransport);

    Assert.NotNull(result);
    Assert.Equal(id, result.id);
  }

  [Theory]
  [MemberData(nameof(TestCases))]
  public async Task Receive_FromRemote_ExistingObjects(string id)
  {
    MemoryTransport localTransport = new();
    Base result = await _operations.Receive(id, _testCaseTransport, localTransport);

    Assert.NotNull(result);
    Assert.Equal(id, result.id);
  }

  [Theory]
  [MemberData(nameof(TestCases))]
  public async Task Receive_FromLocal_OnProgressActionCalled(string id)
  {
    bool wasCalled = false;
    _ = await _operations.Receive(
      id,
      null,
      _testCaseTransport,
      onProgressAction: new UnitTestProgress<ProgressArgs>(_ => wasCalled = true)
    );

    Assert.True(wasCalled);
  }

  public void Dispose()
  {
    // Cleanup resources if necessary
  }
}
