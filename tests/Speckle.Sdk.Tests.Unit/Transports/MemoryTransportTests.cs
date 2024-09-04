using NUnit.Framework;
using Speckle.Sdk.Common;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Tests.Unit.Transports;

[TestFixture]
[TestOf(nameof(MemoryTransport))]
public sealed class MemoryTransportTests : TransportTests
{
  protected override ITransport Sut => _memoryTransport.NotNull();

  private MemoryTransport _memoryTransport;

  [SetUp]
  public void Setup()
  {
    _memoryTransport = new MemoryTransport();
  }
}
