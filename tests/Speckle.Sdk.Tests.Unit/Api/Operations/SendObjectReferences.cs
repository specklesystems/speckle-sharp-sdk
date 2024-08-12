using System.Diagnostics.CodeAnalysis;
using NUnit.Framework;
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
    Base testData = GenerateTestCase(testDepth);
    MemoryTransport transport = new();
    var result = await Speckle.Sdk.Api.Operations.Send(testData, [transport]);

    Assert.That(result.rootObjId, Is.Not.Null);
    Assert.That(result.rootObjId, Has.Length.EqualTo(32));

    Assert.That(result.convertedReferences, Has.Count.EqualTo(testDepth * testDepth));
  }

  private Base GenerateTestCase(int depth)
  {
    var ret = new Base() { applicationId = $"{Guid.NewGuid()}", };
    if (depth <= 0)
    {
      ret["@elements"] = new List<Base> { GenerateTestCase(depth - 1), GenerateTestCase(depth - 1) };
    }

    return ret;
  }
}
