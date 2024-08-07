﻿using NUnit.Framework;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Tests.Unit.Api.Operations;

[TestFixture, TestOf(nameof(Sdk.Api.Operations.Receive))]
public sealed partial class OperationsReceiveTests
{
  static OperationsReceiveTests()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly);
  }

  public static IEnumerable<string> TestCases => s_testObjects.Select(x => x.GetId(true));

  private static readonly Base[] s_testObjects =
  {
    new Base { ["string prop"] = "simple test case", ["numerical prop"] = 123, },
    new Base { ["@detachedProp"] = new Base() { ["the best prop"] = "1234!" } },
    new Base
    {
      ["@detachedList"] = new List<Base> { new Base { ["the worst prop"] = null } },
      ["dictionaryProp"] = new Dictionary<string, Base> { ["dict"] = new Base { ["the best prop"] = "" } },
    },
  };

  private MemoryTransport _testCaseTransport;

  [OneTimeSetUp]
  public async Task GlobalSetup()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly);
    _testCaseTransport = new MemoryTransport();
    foreach (var b in s_testObjects)
    {
      await Sdk.Api.Operations.Send(b, _testCaseTransport, false);
    }
  }

  [Test, TestCaseSource(nameof(TestCases))]
  public async Task Receive_FromLocal_ExistingObjects(string id)
  {
    Base result = await Sdk.Api.Operations.Receive(id, null, _testCaseTransport);

    Assert.That(result.id, Is.EqualTo(id));
  }

  [Test, TestCaseSource(nameof(TestCases))]
  public async Task Receive_FromRemote_ExistingObjects(string id)
  {
    MemoryTransport localTransport = new();
    Base result = await Sdk.Api.Operations.Receive(id, _testCaseTransport, localTransport);

    Assert.That(result.id, Is.EqualTo(id));
  }

  [Test, TestCaseSource(nameof(TestCases))]
  public async Task Receive_FromLocal_OnProgressActionCalled(string id)
  {
    bool wasCalled = false;
    _ = await Sdk.Api.Operations.Receive(id, null, _testCaseTransport, onProgressAction: _ => wasCalled = true);

    Assert.That(wasCalled, Is.True);
  }

  [Test, TestCaseSource(nameof(TestCases))]
  public async Task Receive_FromLocal_OnTotalChildrenCountKnownCalled(string id)
  {
    bool wasCalled = false;
    int children = 0;
    var result = await Sdk.Api.Operations.Receive(
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
