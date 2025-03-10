using FluentAssertions;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;
using Speckle.Sdk.Models.GraphTraversal;

namespace Speckle.Sdk.Tests.Unit.Models.GraphTraversal;

// Mark test class for xUnit
public class TraversalContextExtensionsTests
{
  private TraversalContext? CreateLinkedList(int depth, Func<int, Base> createBaseFunc)
  {
    if (depth <= 0)
    {
      return null;
    }

    return new TraversalContext(createBaseFunc(depth), $"{depth}", CreateLinkedList(depth - 1, createBaseFunc));
  }

  [Theory] // replaces [TestCaseSource]
  [MemberData(nameof(GetTestDepths))]
  public void GetPropertyPath_ReturnsSequentialPath(int depth)
  {
    var testData = CreateLinkedList(depth, i => new Base()).NotNull();

    var path = testData.GetPropertyPath();

    var expected = Enumerable.Range(1, depth).Select(i => i.ToString());

    path.Should().BeEquivalentTo(expected);
  }

  [Theory]
  [MemberData(nameof(GetTestDepths))]
  public void GetAscendant(int depth)
  {
    var testData = CreateLinkedList(depth, i => new Base()).NotNull();

    var all = testData.GetAscendants().ToArray();

    all.Length.Should().Be(depth);
  }

  [Theory]
  [MemberData(nameof(GetTestDepths))]
  public void GetAscendantOfType_AllBase(int depth)
  {
    var testData = CreateLinkedList(depth, i => new Base()).NotNull();

    var all = testData.GetAscendantOfType<Base>().ToArray();

    all.Length.Should().Be(depth);
  }

  [Theory]
  [MemberData(nameof(GetTestDepths))]
  public void GetAscendantOfType_EveryOtherIsCollection(int depth)
  {
    var testData = CreateLinkedList(depth, i => i % 2 == 0 ? new Base() : new Collection()).NotNull();

    var all = testData.GetAscendantOfType<Collection>().ToArray();

    all.Length.Should().Be((int)Math.Ceiling(depth / 2.0));
  }

  // Providing the test depths to [MemberData] for xUnit
  public static IEnumerable<object[]> GetTestDepths() => new[] { 1, 2, 10 }.Select(depth => new object[] { depth });
}
