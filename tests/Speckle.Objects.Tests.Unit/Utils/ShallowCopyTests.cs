using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Speckle.Objects.BuiltElements;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Common;

namespace Objects.Tests.Unit.Utils;

[TestFixture]
public class ShallowCopyTests
{
  [Test]
  public void CanShallowCopy_Wall()
  {
    var wall = new Wall(5, new Line(new Point(0, 0), new Point(3, 0)))
    {
      units = Units.Meters,
      displayValue = new List<Mesh> { new(), new() }
    };

    var shallow = wall.ShallowCopy();
    var displayValue = (IList)shallow["displayValue"].NotNull();
    Assert.That(wall.displayValue, Has.Count.EqualTo(displayValue.Count));
  }
}
