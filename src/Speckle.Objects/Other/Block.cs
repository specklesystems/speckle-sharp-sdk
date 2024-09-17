using Speckle.Objects.Geometry;
using Speckle.Sdk.Common;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Instances;

namespace Speckle.Objects.Other;

/// <summary>
/// Block definition class
/// </summary>
[SpeckleType("Objects.Other.BlockDefinition")]
[Obsolete($"See {nameof(InstanceDefinitionProxy)}")]
public class BlockDefinition : Base
{
  public BlockDefinition() { }

  [SchemaInfo("Block Definition", "A Speckle Block definition")]
  public BlockDefinition(string name, List<Base> geometry, Point? basePoint = null)
  {
    this.name = name;
    this.basePoint = basePoint ?? new(0, 0, 0, Units.None);
    this.geometry = geometry;
  }

  public string name { get; set; }

  /// <summary>
  /// The definition base point of the block
  /// </summary>
  public Point basePoint { get; set; } = new(0, 0, 0, Units.None);

  [DetachProperty]
  public List<Base> geometry { get; set; }

  public string units { get; set; } = Units.None;

  /// <summary>
  /// Returns the translation transform of the base point to the internal origin [0,0,0]
  /// </summary>
  /// <returns></returns>
  public Transform GetBasePointTransform()
  {
    var translation = new Vector(-basePoint.x, -basePoint.y, -basePoint.z, basePoint.units ?? Units.None);
    var transform = new Transform(
      new Vector(1, 0, 0, Units.None),
      new Vector(0, 1, 0, Units.None),
      new Vector(1, 0, 0, Units.None),
      translation
    );
    return transform;
  }
}
