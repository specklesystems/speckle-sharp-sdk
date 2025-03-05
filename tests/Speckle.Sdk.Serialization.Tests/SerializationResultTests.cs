using FluentAssertions;
using Speckle.Sdk.Serialisation;

namespace Speckle.Sdk.Serialization.Tests;

public class SerializationTypeTests
{
  [Fact]
  public void Json()
  {
    var json = new Json("{}");
    json.ToString().Should().Be("{}");
  }
  
  [Fact]
  public void Id()
  {
    var id = new Id("id");
    id.ToString().Should().Be("id");
    id.Equals(new Id("id")).Should().BeTrue();
    id.Equals(new Id("id2")).Should().BeFalse();
    id.GetHashCode().Should().Be("id".GetHashCode());
  }
}
