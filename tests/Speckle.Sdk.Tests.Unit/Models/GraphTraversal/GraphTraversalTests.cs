using System.Collections;
using FluentAssertions;

using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.GraphTraversal;
using Xunit;

namespace Speckle.Sdk.Tests.Unit.Models.GraphTraversal;

public class GraphTraversalTests
{
  public GraphTraversalTests()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(TraversalMock).Assembly);
  }

  private static IEnumerable<TraversalContext> Traverse(Base testCase, params ITraversalRule[] rules)
  {
    var sut = new Sdk.Models.GraphTraversal.GraphTraversal(rules);
    return sut.Traverse(testCase);
  }

  [Fact]
  public void Traverse_TraversesListMembers()
  {
    var traverseListsRule = TraversalRule
      .NewTraversalRule()
      .When(_ => true)
      .ContinueTraversing(x =>
        x.GetMembers(DynamicBaseMemberType.All).Where(p => p.Value is IList).Select(kvp => kvp.Key)
      );

    var expectTraverse = new Base { id = "List Member" };
    var expectIgnored = new Base { id = "Not List Member" };

    TraversalMock testCase = new()
    {
      ListChildren = new List<Base> { expectTraverse },
      DictChildren = new Dictionary<string, Base> { ["myprop"] = expectIgnored },
      Child = expectIgnored,
    };

    var ret = Traverse(testCase, traverseListsRule).Select(b => b.Current).ToList();

    // Assert expected members present
    ret.Should().Contain(testCase);
    ret.Should().Contain(expectTraverse);

    // Assert unexpected members not present
    ret.Should().NotContain(expectIgnored);
    ret.Count.Should().Be(2);
  }

  [Fact]
  public void Traverse_TraversesDictMembers()
  {
    var traverseListsRule = TraversalRule
      .NewTraversalRule()
      .When(_ => true)
      .ContinueTraversing(x =>
        x.GetMembers(DynamicBaseMemberType.All).Where(p => p.Value is IDictionary).Select(kvp => kvp.Key)
      );

    var expectTraverse = new Base { id = "Dict Member" };
    var expectIgnored = new Base { id = "Not Dict Member" };

    TraversalMock testCase = new()
    {
      ListChildren = new List<Base> { expectIgnored },
      DictChildren = new Dictionary<string, Base> { ["myprop"] = expectTraverse },
      Child = expectIgnored,
    };

    var ret = Traverse(testCase, traverseListsRule).Select(b => b.Current).ToList();

    // Assert expected members present
    ret.Should().Contain(testCase);
    ret.Should().Contain(expectTraverse);

    // Assert unexpected members not present
    ret.Should().NotContain(expectIgnored);
    ret.Count.Should().Be(2);
  }

  [Fact]
  public void Traverse_TraversesDynamic()
  {
    var traverseListsRule = TraversalRule
      .NewTraversalRule()
      .When(_ => true)
      .ContinueTraversing(x => x.GetMembers(DynamicBaseMemberType.Dynamic).Select(kvp => kvp.Key));

    var expectTraverse = new Base { id = "List Member" };
    var expectIgnored = new Base { id = "Not List Member" };

    TraversalMock testCase = new()
    {
      Child = expectIgnored,
      ["dynamicChild"] = expectTraverse,
      ["dynamicListChild"] = new List<Base> { expectTraverse },
    };

    var ret = Traverse(testCase, traverseListsRule).Select(b => b.Current).ToList();

    // Assert expected members present
    ret.Should().Contain(testCase);
    ret.Count(x => x == expectTraverse).Should().Be(2);

    // Assert unexpected members not present
    ret.Should().NotContain(expectIgnored);
    ret.Count.Should().Be(3);
  }

  [Fact]
  public void Traverse_ExclusiveRule()
  {
    var expectTraverse = new Base { id = "List Member" };
    var expectIgnored = new Base { id = "Not List Member" };

    var traverseListsRule = TraversalRule
      .NewTraversalRule()
      .When(_ => true)
      .ContinueTraversing(x => x.GetMembers(DynamicBaseMemberType.Dynamic).Select(kvp => kvp.Key));

    TraversalMock testCase = new()
    {
      Child = expectIgnored,
      ["dynamicChild"] = expectTraverse,
      ["dynamicListChild"] = new List<Base> { expectTraverse },
    };

    var ret = Traverse(testCase, traverseListsRule).Select(b => b.Current).ToList();

    // Assert expected members present
    ret.Should().Contain(testCase);
    ret.Count(x => x == expectTraverse).Should().Be(2);

    // Assert unexpected members not present
    ret.Should().NotContain(expectIgnored);
    ret.Count.Should().Be(3);
  }
}
