using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Speckle.Sdk.Api;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Tests.Unit.Api.Operations;

[TestFixture, TestOf(nameof(Sdk.Api.Operations.Receive))]
public sealed partial class OperationsReceiveTests
{
  private static readonly Base[] s_testObjects;
  private IOperations _operations;

  static OperationsReceiveTests()
  {
    Reset();
    s_testObjects =
    [
      new() { ["string prop"] = "simple test case", ["numerical prop"] = 123, },
      new() { ["@detachedProp"] = new Base() { ["the best prop"] = "1234!" } },
      new()
      {
        ["@detachedList"] = new List<Base> { new() { ["the worst prop"] = null } },
        ["dictionaryProp"] = new Dictionary<string, Base> { ["dict"] = new() { ["the best prop"] = "" } },
      }
    ];
  }

  public static IEnumerable<string> TestCases()
  {
    List<string> ret = new();
    foreach (var s in s_testObjects)
    {
      ret.Add(s.GetId(true));
    }

    return ret;
  }

  private MemoryTransport _testCaseTransport;

  private static void Reset()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, Assembly.GetExecutingAssembly());
  }

  [OneTimeSetUp]
  public async Task GlobalSetup()
  {
    Reset();
    var serviceProvider = TestServiceSetup.GetServiceProvider();
    _operations = serviceProvider.GetRequiredService<IOperations>();
    _testCaseTransport = new MemoryTransport();
    foreach (var b in s_testObjects)
    {
      await _operations.Send(b, _testCaseTransport, false);
    }
  }

  [SetUp]
  public void Setup()
  {
    Reset();
    var serviceProvider = TestServiceSetup.GetServiceProvider();
    _operations = serviceProvider.GetRequiredService<IOperations>();
  }

  [Test, TestCaseSource(nameof(TestCases))]
  public async Task Receive_FromLocal_ExistingObjects(string id)
  {
    Base result = await _operations.Receive(id, null, _testCaseTransport);

    Assert.That(result.id, Is.EqualTo(id));
  }

  [Test, TestCaseSource(nameof(TestCases))]
  public async Task Receive_FromRemote_ExistingObjects(string id)
  {
    MemoryTransport localTransport = new();
    Base result = await _operations.Receive(id, _testCaseTransport, localTransport);

    Assert.That(result.id, Is.EqualTo(id));
  }

  [Test, TestCaseSource(nameof(TestCases))]
  public async Task Receive_FromLocal_OnProgressActionCalled(string id)
  {
    bool wasCalled = false;
    _ = await _operations.Receive(
      id,
      null,
      _testCaseTransport,
      onProgressAction: new Progress<ProgressArgs>(_ => wasCalled = true)
    );

    Assert.That(wasCalled, Is.True);
  }

  [Test, TestCaseSource(nameof(TestCases))]
  public async Task Receive_FromLocal_OnTotalChildrenCountKnownCalled(string id)
  {
    bool wasCalled = false;
    int children = 0;
    var result = await _operations.Receive(
      id,
      null,
      _testCaseTransport,
      onTotalChildrenCountKnown: c =>
      {
        wasCalled = true;
        children = c;
      }
    );

    Assert.That(result.id, Is.EqualTo(id));

    var expectedChildren = result.GetTotalChildrenCount() - 1;

    Assert.That(wasCalled, Is.True);
    Assert.That(children, Is.EqualTo(expectedChildren));
  }
}
