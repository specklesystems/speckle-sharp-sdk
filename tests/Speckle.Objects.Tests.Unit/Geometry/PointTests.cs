using System.Diagnostics.CodeAnalysis;
using NUnit.Framework;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Common;

namespace Speckle.Objects.Tests.Unit.Geometry;

[TestFixture, TestOf(typeof(Point))]
public class PointTests
{
  [Test]
  [SuppressMessage(
    "Assertion",
    "NUnit2010:Use EqualConstraint for better assertion messages in case of failure",
    Justification = "Need to explicitly test equality operator"
  )]
  public void TestNull()
  {
    Point? a = null;
    Point? b = null;
    Point c = new(0, 0, 0, Units.Meters);

    Assert.Multiple(() =>
    {
      Assert.That(a == b, Is.True);
      Assert.That(a != b, Is.False);
      Assert.That(b == a, Is.True);
      Assert.That(b != a, Is.False);

      Assert.That(a == c, Is.False);
      Assert.That(a != c, Is.True);
      Assert.That(c == a, Is.False);
      Assert.That(c != a, Is.True);
    });
  }
}
