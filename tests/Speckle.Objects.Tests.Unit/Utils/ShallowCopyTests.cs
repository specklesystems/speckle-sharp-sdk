using System.Collections;
using NUnit.Framework;
using Speckle.Objects.Data;
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
    Assert.That(ds.displayValue, Has.Count.EqualTo(displayValue.Count));
  }
}
