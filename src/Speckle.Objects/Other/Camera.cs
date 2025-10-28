using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Other;

/// <summary>
/// Camera class to represent a perspective camera for a 3D view.
/// </summary>
/// <remarks>Assumes a Z-up, right-handed convention for orientation vectors</remarks>
[SpeckleType("Objects.Other.Camera")]
public class Camera : Base
{
  /// <summary>
  /// The name of the view that is created by this camera
  /// </summary>
  public required string name { get; set; }

  /// <summary>
  /// The location of the camera
  /// </summary>
  public required Point position { get; set; }

  /// <summary>
  /// The unit up vector of the camera
  /// </summary>
  public required Vector up { get; set; }

  /// <summary>
  /// The unit forward vector of the camera
  /// </summary>
  public required Vector forward { get; set; }
}
