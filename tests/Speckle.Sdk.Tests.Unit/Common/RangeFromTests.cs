using Speckle.Sdk.Dependencies;
using Xunit;

namespace Speckle.Sdk.Tests.Unit.Common;

public class RangeFromTests
{
  [Fact]
  public void EnsureRange()
  {
    var list = EnumerableExtensions.RangeFrom(1, 4).ToArray();
    Assert.Equal([1, 2, 3, 4], list);
  }
}
