namespace Speckle.Sdk.Bundles.Handles;

/// <summary>One geometry (SGEO mesh or raw host blob). Appearance is painted through
/// <see cref="BundleBuilder.HasMaterial"/> / <see cref="BundleBuilder.HasColor"/>.</summary>
public sealed class BundleGeometry
{
  internal BundleGeometry(int k, int ord)
  {
    K = k;
    Ord = ord;
  }

  /// <summary>Dense geometry index inside this bundle.</summary>
  public int K { get; }

  /// <summary>Draw order within its owner.</summary>
  public int Ord { get; }

  public override string ToString() => $"geometry {K} (ord {Ord})";
}
