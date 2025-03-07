using FluentAssertions;
using Speckle.Sdk.Models.Extensions;


namespace Speckle.Sdk.Tests.Unit.Models.Extensions;

public class ExceptionTests
{
  [Fact]
  public void CanPrintAllInnerExceptions()
  {
    // Test with a single exception
    var ex = new Exception("Some error");
    var exMsg = ex.ToFormattedString();
    exMsg.Should().NotBeNull();

    // Test with an inner exception
    var ex2 = new Exception("One or more errors occurred", ex);
    var ex2Msg = ex2.ToFormattedString();
    ex2Msg.Should().NotBeNull();

    // Test with an aggregate exception
    var ex3 = new AggregateException("One or more errors occurred", ex2);
    var ex3Msg = ex3.ToFormattedString();
    ex3Msg.Should().NotBeNull();
  }
}
