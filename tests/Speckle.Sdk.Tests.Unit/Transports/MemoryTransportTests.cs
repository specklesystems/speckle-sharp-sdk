// MemoryTransportTests.cs

using Shouldly;
using Speckle.Sdk.Common;
using Speckle.Sdk.Transports;
using Xunit;

namespace Speckle.Sdk.Tests.Unit.Transports;

public sealed class MemoryTransportTests : TransportTests
{
  protected override ITransport Sut => _memoryTransport.NotNull();
  private MemoryTransport _memoryTransport;

  // Constructor used for setup in xUnit
  public MemoryTransportTests()
  {
    _memoryTransport = new MemoryTransport();
  }

  [Fact]
  public void TransportName_ShouldBeSetProperly()
  {
    // Example test showing usage of Shouldly
    _memoryTransport.TransportName.ShouldBe("Memory");
  }
}
