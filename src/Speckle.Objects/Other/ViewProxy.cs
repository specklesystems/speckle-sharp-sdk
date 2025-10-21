using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Proxies;

namespace Speckle.Objects.Other;

/// <summary>
/// Proxy for 3D views.
/// </summary>
/// <remarks>The <see cref="objects"/> list points to the applicationIds of any atomic objects that are visible in this view. An empty objects list indicates that all objects by default are visible.</remarks>
[SpeckleType("Objects.Other.ViewProxy")]
public class ViewProxy : Base, IProxyCollection
{
  /// <summary>
  /// The list of application ids of objects that belong to this view
  /// </summary>
  public required List<string> objects { get; set; }

  /// <summary>
  /// The camera used for this view
  /// </summary>
  public required Camera camera { get; set; }

  /// <summary>
  /// The name of this view
  /// </summary>
  public required string name { get; set; }
}
