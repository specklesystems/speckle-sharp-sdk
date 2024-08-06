namespace Speckle.Core.Kits;

public readonly struct HostApplication
{
  public string Name { get; }
  public string Slug { get; }

  public HostApplication(string name, string slug)
  {
    Name = name;
    Slug = slug;
  }

  public string GetVersion(HostAppVersion version) => version.ToString().TrimStart('v');
}
