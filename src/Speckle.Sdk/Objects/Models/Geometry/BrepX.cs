using Speckle.Objects.Other;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Geometry;

public interface IRawEncodedObject
{
  public RawEncoding encodedValue { get; set; }
}

public abstract class RawEncodedObject : Base, IDisplayValue<List<Mesh>>, IRawEncodedObject, IHasArea, IHasVolume
{
  [DetachProperty]
  public required List<Mesh> displayValue { get; set; }

  [DetachProperty]
  public required RawEncoding encodedValue { get; set; }

  public required string units { get; set; }

  public double area { get; set; }

  public double volume { get; set; }
}

[SpeckleType("Objects.Geometry.BrepX")]
public class BrepX : RawEncodedObject;

[SpeckleType("Objects.Geometry.ExtrusionX")]
public class ExtrusionX : RawEncodedObject;

[SpeckleType("Objects.Geometry.SubDX")]
public class SubDX : RawEncodedObject;
