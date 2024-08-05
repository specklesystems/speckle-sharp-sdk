using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Speckle.Core.Common;
using Speckle.Core.Kits;
using Speckle.Objects.BuiltElements;
using Speckle.Objects.Geometry;

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
