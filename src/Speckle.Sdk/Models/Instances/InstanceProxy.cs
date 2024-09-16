using Speckle.DoubleNumerics;

namespace Speckle.Sdk.Models.Instances;

/// <summary>
/// A proxy class for an instance (e.g, a rhino block, or an autocad block reference).
/// </summary>
[SpeckleType("Speckle.Core.Models.Instances.InstanceProxy")]
public class InstanceProxy : Base, IInstanceComponent
{
  /// <summary>
  /// The definition id as present in the original host app. On receive, it will be mapped to the newly created definition id.
  /// </summary>
  public required string definitionId { get; set; }

  /// <summary>
  /// The transform of the instance reference.
  /// </summary>
  public required Matrix4x4 transform { get; set; }

  /// <summary>
  /// The units of the host application file.
  /// </summary>
  public required string units { get; set; }

  public required int maxDepth { get; set; }
}
