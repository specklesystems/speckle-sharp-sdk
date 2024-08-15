﻿using NUnit.Framework;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Tests.Unit.Api.Operations;

public class SendObjectReferences
{
  [TestCase(0)]
  [TestCase(1)]
  [TestCase(10)]
  public async Task SendObjectsWithApplicationIds(int testDepth)
  {
    Base testData = GenerateTestCase(testDepth, true);
    MemoryTransport transport = new();
    var result = await Speckle.Sdk.Api.Operations.Send(testData, [transport]);

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
    var result = await Speckle.Sdk.Api.Operations.Send(testData, [transport]);

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