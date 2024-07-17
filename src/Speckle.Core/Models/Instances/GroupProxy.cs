namespace Speckle.Core.Models.Instances;

/// <summary>
/// Grouped objects with a meaningful way for host application so use this proxy if you want to group object references for any purpose.
/// i.e. in rhino -> creating group make objects selectable/moveable/editable together.
/// </summary>
public class GroupProxy : Base, IProxyCollection
{
  public List<string> objects { get; set; }

  public string name { get; set; }
}
