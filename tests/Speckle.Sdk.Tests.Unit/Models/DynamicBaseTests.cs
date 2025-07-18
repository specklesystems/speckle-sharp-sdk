using FluentAssertions;
using Microsoft.CSharp.RuntimeBinder;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Tests.Unit.Models;

public class DynamicBaseTests
{
  public DynamicBaseTests()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(BaseTests).Assembly);
  }

  [Fact]
  public void Indexer_SetAndGet()
  {
    // Arrange
    var dynamicBase = new DynamicBase();
    var key = "testProperty";
    var value = "testValue";

    // Act
    dynamicBase[key] = value;
    var result = dynamicBase[key];

    // Assert
    result.Should().Be(value);
  }

  [Fact]
  public void DynamicProperty_SetAndGet()
  {
    // Arrange
    dynamic dynamicBase = new DynamicBase();
    var value = "dynamicValue";

    // Act
    dynamicBase.dynamicProperty = value;
    object result = dynamicBase.dynamicProperty;

    // Assert
    result.Should().Be(value);
  }

  [Fact]
  public void GetMembers_Default()
  {
    // Arrange
    dynamic dynamicBase = new DynamicBase();
    dynamicBase.dynamicProp = "hello";

    // Act
    IDictionary<string, object?> members = dynamicBase.GetMembers();

    // Assert
    members.Should().ContainKey("dynamicProp");
  }

  [Fact]
  public void GetMembers_Instance()
  {
    // Arrange
    var dynamicBase = new TestDynamicBase();

    // Act
    var members = dynamicBase.GetMembers(DynamicBaseMemberType.Instance);

    // Assert
    members.Should().ContainKey(nameof(TestDynamicBase.InstanceProperty));
    members.Should().NotContainKey("dynamicProp");
  }

  [Fact]
  public void GetDynamicMemberNames()
  {
    // Arrange
    dynamic dynamicBase = new DynamicBase();
    dynamicBase.prop1 = 1;
    dynamicBase.prop2 = "test";

    // Act
    IEnumerable<string> memberNames = dynamicBase.GetDynamicMemberNames();

    // Assert
    memberNames.Should().BeEquivalentTo(["DynamicPropertyKeys", "prop1", "prop2"]);
  }

  [Fact]
  public void TryGetMember_Existing()
  {
    // Arrange
    dynamic dynamicBase = new DynamicBase();
    dynamicBase.existingProp = "I exist";

    // Act
    var result = dynamicBase.existingProp;

    // Assert
    ((object)result)
      .Should()
      .Be("I exist");
  }

  [Fact]
  public void TryGetMember_NonExisting()
  {
    // Arrange
    dynamic dynamicBase = new DynamicBase();

    // Act

    var exception = Assert.Throws<RuntimeBinderException>(() =>
    {
      var result = dynamicBase.nonExistingProp;
    });
    // Assert
    exception.Should().NotBeNull();
  }

  [Fact]
  public void TrySetMember()
  {
    // Arrange
    dynamic dynamicBase = new DynamicBase();

    // Act
    dynamicBase.newProp = "newValue";

    // Assert
    ((object)dynamicBase.newProp)
      .Should()
      .Be("newValue");
  }

  private class TestDynamicBase : DynamicBase
  {
    public string InstanceProperty { get; set; } = "instance";
  }
}
