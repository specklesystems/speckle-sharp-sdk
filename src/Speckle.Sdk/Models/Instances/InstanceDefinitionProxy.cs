using Speckle.Sdk.Models.Proxies;

namespace Speckle.Sdk.Models.Instances;

/// <summary>
/// A proxy class for an instance definition.
/// </summary>
[SpeckleType("Speckle.Core.Models.Instances.InstanceDefinitionProxy")]
public class InstanceDefinitionProxy : Base, IInstanceComponent, IProxyCollection
{
  /// <summary>
  /// The original ids of the objects that are part of this definition, as present in the source host app. On receive, they will be mapped to corresponding newly created definition ids.
  /// </summary>
  public required List<string> objects { get; set; } // source app application ids for the objects

  public required int maxDepth { get; set; }

  /// <summary>
  /// Name of the instance definition proxy collection which is unique for rhino, autocad and sketchup
  /// </summary>
  public required string name { get; set; }
}
