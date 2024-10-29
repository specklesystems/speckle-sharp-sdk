using System.Collections;
using NUnit.Framework;
using Speckle.Objects.BuiltElements;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Common;

namespace Speckle.Objects.Tests.Unit.Utils;

[TestFixture]
public class ShallowCopyTests
{
  [Test]
  public void CanShallowCopy_Wall()
  {
    const string UNITS = Units.Meters;
    var wall = new Wall()
    {
      height = 5,
      baseLine = new Line()
      {
        start = new Point(0, 0, 0, UNITS),
        end = new Point(3, 0, 0, UNITS),
        units = UNITS,
      },
      units = UNITS,
      displayValue = new List<Mesh>
      {
        new Mesh
        {
          vertices = new(),
          faces = new(),
          units = UNITS,
        },
        new Mesh
        {
          vertices = new(),
          faces = new(),
          units = UNITS,
        },
      },
    };

    var shallow = wall.ShallowCopy();
    var displayValue = (IList)shallow["displayValue"].NotNull();
    Assert.That(wall.displayValue, Has.Count.EqualTo(displayValue.Count));
  }
}
