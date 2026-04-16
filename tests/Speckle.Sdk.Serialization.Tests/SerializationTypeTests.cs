using AwesomeAssertions;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Serialisation.V2.Send;

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

  [Fact]
  public void BaseItem()
  {
    var id = new Id("id");
    var json = new Json("{}");
    var baseItem = new BaseItem(id, json, false, new Dictionary<Id, int>());
    baseItem.Equals(new BaseItem(id, json, false, new Dictionary<Id, int>())).Should().BeTrue();
    baseItem.Equals(new BaseItem(new Id("id2"), json, false, new Dictionary<Id, int>())).Should().BeFalse();
    baseItem.GetHashCode().Should().Be(id.GetHashCode());
  }
}
