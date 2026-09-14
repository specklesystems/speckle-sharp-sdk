namespace Speckle.Sdk.Bundles.Handles;

/// <summary>The property carrier a host element becomes. Relations, geometry and placements are written through
/// <see cref="BundleBuilder"/>; this type only identifies the object.</summary>
public sealed class BundleObject
{
  internal BundleObject(int k, string applicationId)
  {
    K = k;
    ApplicationId = applicationId;
  }

  /// <summary>Dense object index inside this bundle.</summary>
  public int K { get; }

  public string ApplicationId { get; }

  public string? Name { get; internal set; }

  public override string ToString() => Name is null ? ApplicationId : $"{Name} ({ApplicationId})";
}
