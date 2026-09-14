using SpecMaterial = Speckle.Bundle.Spec.Material;

namespace Speckle.Sdk.Bundles.Handles;

public sealed class BundleMaterial : BundleNode
{
  internal BundleMaterial(int k, SpecMaterial fields)
    : base(k)
  {
    Fields = fields;
  }

  /// <summary>The MATERIAL row this handle points at.</summary>
  public SpecMaterial Fields { get; }
}
