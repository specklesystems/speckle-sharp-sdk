using System.Collections.Concurrent;
using NUnit.Framework;
using Speckle.Sdk.Common;
using Speckle.Sdk.Transports;

namespace Speckle.Core.Tests.Unit.Transports;

[TestFixture]
[TestOf(nameof(MemoryTransport))]
public sealed class MemoryTransportTests : TransportTests
{
  protected override ITransport Sut => _memoryTransport.NotNull();

  private MemoryTransport _memoryTransport;

  [SetUp]
  public void Setup()
  {
    _memoryTransport = new MemoryTransport(new ConcurrentDictionary<string, string>());
  }
}
