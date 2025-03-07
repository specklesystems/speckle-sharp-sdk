using FluentAssertions;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Extensions;


namespace Speckle.Sdk.Tests.Unit.Models;

public class TraversalTests
{
  [Fact(DisplayName = "Tests that provided breaker rules are respected")]
  public void TestFlattenWithBreaker()
  {
    //Setup
    Base root = new()
    {
      id = "root",
      ["child"] = new Base
      {
        id = "traverse through me",
        ["child"] = new Base
        {
          id = "break on me, go no further",
          ["child"] = new Base { id = "should have ignored me" },
        },
      },
    };

    static bool BreakRule(Base b) => b.id.NotNull().Contains("break on me");

    //Flatten
    var ret = root.Flatten(BreakRule).ToList();

    //Test
    ret.Count.Should().Be(3);

    ret.Should().OnlyHaveUniqueItems();

    ret.Where(BreakRule).Should().NotBeEmpty();

    ret.Should().NotContain(x => x.id == "should have ignored me");
  }

  [Theory(DisplayName = "Tests breaking after a fixed number of items")]
  [InlineData(5, 5)]
  [InlineData(5, 10)]
  [InlineData(10, 5)]
  public void TestBreakerFixed(int nestDepth, int flattenDepth)
  {
    //Setup
    Base rootObject = new() { id = "0" };
    Base lastNode = rootObject;
    for (int i = 1; i < nestDepth; i++)
    {
      Base newNode = new() { id = $"{i}" };
      lastNode["child"] = newNode;
      lastNode = newNode;
    }

    //Flatten
    int counter = 0;
    var ret = rootObject.Flatten(_ => ++counter >= flattenDepth).ToList();

    //Test
    ret.Count.Should().Be(Math.Min(flattenDepth, nestDepth));

    ret.Should().OnlyHaveUniqueItems();
  }

  [Fact(DisplayName = "Tests that the flatten function does not get stuck on circular references")]
  public void TestCircularReference()
  {
    //Setup
    Base objectA = new() { id = "a" };
    Base objectB = new() { id = "b" };
    Base objectC = new() { id = "c" };

    objectA["child"] = objectB;
    objectB["child"] = objectC;
    objectC["child"] = objectA;

    //Flatten
    var ret = objectA.Flatten().ToList();

    //Test
    ret.Should().OnlyHaveUniqueItems();

    ret.Should().BeEquivalentTo([objectA, objectB, objectC]);

    ret.Count.Should().Be(3);
  }

  [Fact(DisplayName = "Tests that the flatten function correctly handles (non circular) duplicates")]
  public void TestDuplicates()
  {
    //Setup
    Base objectA = new() { id = "a" };
    Base objectB = new() { id = "b" };

    objectA["child1"] = objectB;
    objectA["child2"] = objectB;

    //Flatten
    var ret = objectA.Flatten().ToList();

    //Test
    ret.Should().OnlyHaveUniqueItems();

    ret.Should().BeEquivalentTo([objectA, objectB]);

    ret.Count.Should().Be(2);
  }
}
