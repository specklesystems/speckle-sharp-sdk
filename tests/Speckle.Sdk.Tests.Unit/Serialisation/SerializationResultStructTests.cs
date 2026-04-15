using AwesomeAssertions;
using Speckle.Sdk.Serialisation;

namespace Speckle.Sdk.Tests.Unit.Serialisation;

public class SerializationResultStructTests
{
  [Fact]
  public void Id_Should_Throw_On_Null()
  {
#pragma warning disable CA1806
    Action act = () => new Id(null!);
#pragma warning restore CA1806
    act.Should().Throw<ArgumentNullException>();
  }

  [Fact]
  public void Id_Should_Store_Value()
  {
    var id = new Id("abc");
    id.Value.Should().Be("abc");
    id.ToString().Should().Be("abc");
  }

  [Fact]
  public void Id_Equality_Should_Not_Be_Case_Insensitive()
  {
    var id1 = new Id("abc");
    var id2 = new Id("ABC");
    id1.Equals(id2).Should().BeFalse();
  }

  [Fact]
  public void Json_Should_Store_Value()
  {
    var json = new Json("{\"foo\":1}");
    json.Value.Should().Be("{\"foo\":1}");
    json.ToString().Should().Be("{\"foo\":1}");
  }

  [Fact]
  public void Json_Should_Allow_Empty_String()
  {
    var json = new Json("");
    json.Value.Should().Be("");
  }

  [Fact]
  public void Json_Should_Allow_Null_String()
  {
#pragma warning disable CA1806
    Action act = () => new Json(null!);
#pragma warning restore CA1806
    act.Should().Throw<ArgumentNullException>();
  }
}
