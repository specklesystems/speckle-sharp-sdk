namespace Speckle.Sdk.Models.Proxies;

/// <summary>
/// Represents a color that is found on objects and collections in a root collection
/// </summary>
[SpeckleType("Speckle.Core.Models.Proxies.ColorProxy")]
public class ColorProxy : Base, IProxyCollection
{
  public ColorProxy() { }

  public ColorProxy(int color, string applicationId, string? name)
  {
    value = color;
    this.applicationId = applicationId;
    this.name = name;
  }

  public List<string> objects { get; set; }

  /// <summary>
  /// The argb int of the color
  /// </summary>
  public int value { get; set; }

  /// <summary>
  /// The name, if any, of the color
  /// </summary>
  public string? name { get; set; }
}
