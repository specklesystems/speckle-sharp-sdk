using System.Collections;
using NUnit.Framework;
using Speckle.Objects.BuiltElements.Revit;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Tests.Unit.Utils;

[TestFixture]
public class ShallowCopyTests
{
  [Test]
  public void CanShallowCopy_Wall()
  {
    const string UNITS = Units.Meters;
    var ds = new DirectShape()
    {
      name = "directShape",
      units = UNITS,
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
    };

    var shallow = ds.ShallowCopy();
    var displayValue = (IList)shallow["displayValue"].NotNull();
    Assert.That(ds.displayValue, Has.Count.EqualTo(displayValue.Count));
  }
}
