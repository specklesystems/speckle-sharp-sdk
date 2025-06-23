using Speckle.Objects.Data;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Proxies;

namespace Speckle.Objects.Other;

/// <summary>
/// Proxy for levels as DataObject value.
/// <remarks> These proxy lives in Objects library because it depends on DataObject</remarks>
/// </summary>
[SpeckleType("Objects.Other.LevelProxy")]
public class LevelProxy : Base, IProxyCollection
{
  /// <summary>
  /// The list of application ids of objects that use this level
  /// </summary>
  public required List<string> objects { get; set; }
  
  public required DataObject value { get; set; }
}
