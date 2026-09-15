namespace Speckle.Sdk.Bundles.Handles;

/// <summary>The property carrier a host element becomes. Relations, geometry and placements are written through
/// <see cref="BundleBuilder"/>, which is the only thing that reads or writes the ordinals and single-valued slots
/// this type carries.</summary>
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

  internal List<BundleGeometry>? Geometries;
  internal int DisplayOrd;
  internal int SolidOrd;
  internal int CenterlineOrd;
  internal int PlacementOrd;
  internal int ChildOrd;
  internal int AssemblyMemberOrd;
  internal bool PropertiesWritten;
  internal BundleContainer? Collection;
  internal BundleContainer? Model;
  internal BundleContainer? System;
  internal BundleLevel? Level;
  internal BundleMaterial? Material;
  internal BundleColor? Color;
  internal BundleObject? Parent;
  internal BundleObject? Assembly;
  internal BundleObject? Host;
  internal BundleObject? Room;

  public override string ToString() => Name is null ? ApplicationId : $"{Name} ({ApplicationId})";
}
