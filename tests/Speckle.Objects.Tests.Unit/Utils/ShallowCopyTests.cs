using System.Collections;
using FluentAssertions;
using Speckle.Objects.Data;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Common;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Tests.Unit.Utils;

public class ShallowCopyTests
{
  public ShallowCopyTests()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(Point).Assembly);
  }

  [Fact]
  public void CanShallowCopy_Wall()
  {
    const string UNITS = Units.Meters;
    var ds = new DataObject()
    {
      name = "directShape",
      displayValue = new List<Base>
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
      properties = new Dictionary<string, object?>(),
    };

    var shallow = ds.ShallowCopy();
    var displayValue = (IList)shallow["displayValue"].NotNull();
    ds.displayValue.Count.Should().Be(displayValue.Count);
  }
}
