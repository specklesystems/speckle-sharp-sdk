using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;
using Speckle.Sdk.Api;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports;
using Xunit;

namespace Speckle.Sdk.Tests.Unit.Api.Operations;

public sealed partial class OperationsReceiveTests
{
  private static readonly Base[] s_testObjects;
  private IOperations _operations;

  static OperationsReceiveTests()
  {
    Reset();
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

  public static IEnumerable<object[]> TestCases()
  {
    List<object[]> ret = new();
    foreach (var s in s_testObjects)
    {
      ret.Add([s.GetId(true)]);
    }

    return ret;
  }

  private MemoryTransport _testCaseTransport;

  private static void Reset()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, Assembly.GetExecutingAssembly());
  }

  public OperationsReceiveTests()
  {
    Reset();
    var serviceProvider = TestServiceSetup.GetServiceProvider();
    _operations = serviceProvider.GetRequiredService<IOperations>();
    _testCaseTransport = new MemoryTransport();
    foreach (var b in s_testObjects)
    {
      _operations.Send(b, _testCaseTransport, false).Wait();
    }
  }

  [Theory, MemberData(nameof(TestCases))]
  public async Task Receive_FromLocal_ExistingObjects(string id)
  {
    Base result = await _operations.Receive(id, null, _testCaseTransport);

    result.id.ShouldBe(id);
  }

  [Theory, MemberData(nameof(TestCases))]
  public async Task Receive_FromRemote_ExistingObjects(string id)
  {
    MemoryTransport localTransport = new();
    Base result = await _operations.Receive(id, _testCaseTransport, localTransport);

    result.id.ShouldBe(id);
  }

  [Theory, MemberData(nameof(TestCases))]
  public async Task Receive_FromLocal_OnProgressActionCalled(string id)
  {
    bool wasCalled = false;
    _ = await _operations.Receive(
      id,
      null,
      _testCaseTransport,
      onProgressAction: new UnitTestProgress<ProgressArgs>(_ => wasCalled = true)
    );

    wasCalled.ShouldBeTrue();
  }
}
