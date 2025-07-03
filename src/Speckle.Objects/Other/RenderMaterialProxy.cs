using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Proxies;

namespace Speckle.Objects.Other;

/// <summary>
/// Used to store render material to object relationships in root collections
/// <remarks> These proxy lives in Objects library because it depends on RenderMaterial</remarks>
/// </summary>
[SpeckleType("Objects.Other.RenderMaterialProxy")]
public class RenderMaterialProxy : Base, IProxyCollection
{
  /// <summary>
  /// The list of application ids of objects that use this render material
  /// </summary>
  public required List<string> objects { get; set; }

  /// <summary>
  /// The render material used by <see cref="objects"/>
  /// </summary>
  public required RenderMaterial value { get; set; }
}
