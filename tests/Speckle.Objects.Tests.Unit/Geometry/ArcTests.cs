using System;
using NUnit.Framework;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Common;

namespace Objects.Tests.Unit.Geometry;

[TestFixture, TestOf(typeof(Arc))]
public class ArcTests
{
  private Plane TestPlane =>
    new()
    {
      origin = new Point(0, 0),
      normal = new Vector(0, 0, 1),
      xdir = new Vector(1, 0, 0),
      ydir = new Vector(0, 1, 0),
      units = Units.Meters,
    };

  [Test]
  public void CanCreateArc_HalfCircle()
  {
    var arc = new Arc(TestPlane, new Point(-5, 5), new Point(5, 5), Math.PI);

    Assert.That(arc.startAngle, Is.EqualTo(0));
    Assert.That(arc.endAngle, Is.EqualTo(Math.PI));

    Assert.That(Point.Distance(arc.midPoint, new Point(0, 0)), Is.EqualTo(0).Within(0.0001));
    Assert.That(Point.Distance(arc.plane.origin, new Point(0, 5)), Is.EqualTo(0).Within(0.0001));
  }
}
