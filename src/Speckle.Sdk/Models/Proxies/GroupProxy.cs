namespace Speckle.Sdk.Models.Proxies;

/// <summary>
/// Grouped objects with a meaningful way for host application so use this proxy if you want to group object references for any purpose.
/// i.e. in rhino -> creating group make objects selectable/moveable/editable together.
/// </summary>
[SpeckleType("Speckle.Core.Models.Proxies.GroupProxy")]
public class GroupProxy : Base, IProxyCollection
{
  public List<string> objects { get; set; }

  /// <summary>
  /// Name of the group proxy collection which is unique for rhino, autocad and sketchup
  /// </summary>
  public string name { get; set; }
}
