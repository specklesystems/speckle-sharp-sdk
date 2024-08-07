﻿using NUnit.Framework;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;
using Speckle.Sdk.Models.GraphTraversal;

namespace Speckle.Sdk.Tests.Unit.Models.GraphTraversal;

[TestOf(typeof(TraversalContextExtensions))]
public class TraversalContextExtensionsTests
{
  public static int[] TestDepths => new[] { 1, 2, 10 };

  private TraversalContext? CreateLinkedList(int depth, Func<int, Base> createBaseFunc)
  {
    if (depth <= 0)
      return null;
    return new TraversalContext(createBaseFunc(depth), $"{depth}", CreateLinkedList(depth - 1, createBaseFunc));
  }

  [TestCaseSource(nameof(TestDepths))]
  public void GetPropertyPath_ReturnsSequentialPath(int depth)
  {
    var testData = CreateLinkedList(depth, i => new()).NotNull();

    var path = TraversalContextExtensions.GetPropertyPath(testData);

    var expected = Enumerable.Range(1, depth).Select(i => i.ToString());

    Assert.That(path, Is.EquivalentTo(expected));
  }

  [TestCaseSource(nameof(TestDepths))]
  public void GetAscendant(int depth)
  {
    var testData = CreateLinkedList(depth, i => new()).NotNull();

    var all = TraversalContextExtensions.GetAscendants(testData).ToArray();

    Assert.That(all, Has.Length.EqualTo(depth));
  }

  [TestCaseSource(nameof(TestDepths))]
  public void GetAscendantOfType_AllBase(int depth)
  {
    var testData = CreateLinkedList(depth, i => new()).NotNull();

    var all = TraversalContextExtensions.GetAscendantOfType<Base>(testData).ToArray();

    Assert.That(all, Has.Length.EqualTo(depth));
  }

  [TestCaseSource(nameof(TestDepths))]
  public void GetAscendantOfType_EveryOtherIsCollection(int depth)
  {
    var testData = CreateLinkedList(depth, i => i % 2 == 0 ? new Base() : new Collection()).NotNull();

    var all = TraversalContextExtensions.GetAscendantOfType<Collection>(testData).ToArray();

    Assert.That(all, Has.Length.EqualTo(Math.Ceiling(depth / 2.0)));
  }
}
