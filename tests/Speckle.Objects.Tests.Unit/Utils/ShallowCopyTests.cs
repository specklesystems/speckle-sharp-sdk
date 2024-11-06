using System.Collections;
using NUnit.Framework;
using Shouldly;
using Speckle.Objects.BuiltElements;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Common;
using Xunit;

namespace Speckle.Objects.Tests.Unit.Utils;

public class ShallowCopyTests
{
  [Fact]
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
    wall.displayValue.Count.ShouldBe(displayValue.Count);
  }
}
