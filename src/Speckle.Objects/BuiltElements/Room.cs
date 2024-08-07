using Speckle.Objects.BuiltElements.Revit;
using Speckle.Objects.Geometry;
using Speckle.Objects.Utils;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements;

[SpeckleType("Objects.BuiltElements.Room")]
public class Room : Base, IHasArea, IHasVolume, IDisplayValue<List<Mesh>>
{
  public Room() { }

  /// <summary>
  /// SchemaBuilder constructor for a Room
  /// </summary>
  /// <remarks>Assign units when using this constructor due to <see cref="height"/> prop</remarks>
  [SchemaInfo("Room", "Creates a Speckle room", "BIM", "Architecture")]
  public Room(string name, string number, Level level, [SchemaMainParam] Point basePoint)
  {
    this.name = name;
    this.number = number;
    this.level = level;
    this.basePoint = basePoint;
  }

  /// <summary>
  /// SchemaBuilder constructor for a Room
  /// </summary>
  /// <remarks>Assign units when using this constructor due to <see cref="height"/> prop</remarks>
  [SchemaInfo("RevitRoom", "Creates a Revit room with parameters", "Revit", "Architecture")]
  public Room(
    string name,
    string number,
    Level level,
    [SchemaMainParam] Point basePoint,
    List<Parameter>? parameters = null
  )
  {
    this.name = name;
    this.number = number;
    this.level = level;
    this.basePoint = basePoint;
    this["parameters"] = parameters?.ToBase();
  }

  public string name { get; set; }
  public string number { get; set; }
  public virtual Level? level { get; set; }
  public Point basePoint { get; set; }
  public double height { get; set; }
  public List<ICurve> voids { get; set; } = new();
  public ICurve outline { get; set; }

  public string units { get; set; }

  [DetachProperty]
  public List<Mesh> displayValue { get; set; }

  public double area { get; set; }
  public double volume { get; set; }
}
