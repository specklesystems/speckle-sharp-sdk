namespace Speckle.Core.Serialisation.TypeCache;

internal class VersionCache
{
  public string Type { get; private set; }
  public List<(Version, CachedTypeInfo)> Versions { get; private set; } = new();
  public CachedTypeInfo? LatestVersion;

  public VersionCache(string type)
  {
    Type = type;
  }

  public void SortVersions()
  {
    // for some reason I can't get the tuple deconstructed (but it IS rather late)
    Versions = Versions.OrderBy(v => v.Item1).ToList();
  }
}
