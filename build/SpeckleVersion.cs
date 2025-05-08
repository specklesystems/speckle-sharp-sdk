using System.Diagnostics.CodeAnalysis;
using NuGet.Versioning;

namespace build;

public class SpeckleVersion : IEquatable<SpeckleVersion>, IComparable<SpeckleVersion>
{
  private NuGetVersion _nuGetVersion;

  public SpeckleVersion(Version version)
    : this(new NuGetVersion(version)) { }

  public SpeckleVersion(
    int major,
    int minor,
    int patch,
    string? channel = null,
    int? release = null
  )
  {
    if (channel is not null)
    {
      if (release is not null)
      {
        if (release <= 0)
        {
          _nuGetVersion = new NuGetVersion(major, minor, patch, [channel], null);
          return;
        }
        _nuGetVersion = new NuGetVersion(major, minor, patch, [channel, release.ToString()!], null);
      }
      else
      {
        _nuGetVersion = new NuGetVersion(major, minor, patch, [channel], null);
      }
    }
    else
    {
      _nuGetVersion = new NuGetVersion(major, minor, patch);
    }
  }

  public SpeckleVersion(NuGetVersion nuGetVersion)
  {
    _nuGetVersion = nuGetVersion;
  }

  public Version Version => _nuGetVersion.Version;

  public string Channel
  {
    get => StableChannel();
    set => _nuGetVersion =
      new NuGetVersion(_nuGetVersion.Major, _nuGetVersion.Minor, _nuGetVersion.Patch, [value], null);

  }

  public string Release
  {
    get =>ZeroRelease();
    set => _nuGetVersion =
      new NuGetVersion(_nuGetVersion.Major, _nuGetVersion.Minor, _nuGetVersion.Patch, [Channel, value], null);
  }

  private string StableChannel()
  {
    var channel = _nuGetVersion.ReleaseLabels.FirstOrDefault();
    if (string.IsNullOrWhiteSpace(channel))
    {
      return "stable";
    }
    return channel;
  }
  
  private string ZeroRelease()
  {
    var channel = _nuGetVersion.ReleaseLabels.Skip(1).FirstOrDefault();
    if (string.IsNullOrWhiteSpace(channel))
    {
      return "0";
    }
    return channel;
  }

  public static SpeckleVersion Parse(string version) => new(NuGetVersion.Parse(version));

  public static bool TryParse(
    string? version,
    [NotNullWhen(true)] out SpeckleVersion? speckleVersion
  )
  {
    if (NuGetVersion.TryParse(version, out var nuGetVersion))
    {
      speckleVersion = new SpeckleVersion(nuGetVersion);
      return true;
    }
    speckleVersion = null;
    return false;
  }

  public override string ToString() => _nuGetVersion.ToString();

  public bool GreaterThan(SpeckleVersion other) => Version > other.Version;

  public static bool GreaterThan(SpeckleVersion a, SpeckleVersion b) => a.Version > b.Version;

  public bool Equals(SpeckleVersion? other)
  {
    if (other is null)
    {
      return false;
    }

    if (ReferenceEquals(this, other))
    {
      return true;
    }

    return _nuGetVersion.Equals(other._nuGetVersion);
  }

  public override bool Equals(object? obj)
  {
    if (obj is null)
    {
      return false;
    }

    if (ReferenceEquals(this, obj))
    {
      return true;
    }

    if (obj.GetType() != GetType())
    {
      return false;
    }

    return Equals((SpeckleVersion)obj);
  }

  public override int GetHashCode() => _nuGetVersion.GetHashCode();

  public int CompareTo(SpeckleVersion? other)
  {
    if (ReferenceEquals(this, other))
    {
      return 0;
    }

    if (other is null)
    {
      return 1;
    }

    return _nuGetVersion.CompareTo(other._nuGetVersion);
  }

  public static bool operator ==(SpeckleVersion? left, SpeckleVersion? right)
  {
    if (left is null)
    {
      return right is null;
    }

    return left.Equals(right);
  }

  public static bool operator !=(SpeckleVersion? left, SpeckleVersion? right) => !(left == right);

  public static bool operator <(SpeckleVersion? left, SpeckleVersion? right) =>
    left is null ? right is not null : left.CompareTo(right) < 0;

  public static bool operator <=(SpeckleVersion? left, SpeckleVersion? right) =>
    left is null || left.CompareTo(right) <= 0;

  public static bool operator >(SpeckleVersion? left, SpeckleVersion? right) =>
    left is not null && left.CompareTo(right) > 0;

  public static bool operator >=(SpeckleVersion? left, SpeckleVersion? right) =>
    left is null ? right is null : left.CompareTo(right) >= 0;
}
