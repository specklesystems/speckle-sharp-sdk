using SpecMaterial = Speckle.Bundle.Spec.Material;

namespace Speckle.Sdk.Bundles.Handles;

public sealed class BundleMaterial : BundleNode
{
  internal BundleMaterial(BundleBuilder builder, int k, string key, SpecMaterial fields)
    : base(builder, k)
  {
    Key = key;
    Fields = fields;
  }

  /// <summary>The intern key this material was added under.</summary>
  public string Key { get; }

  /// <summary>The MATERIAL row this handle points at.</summary>
  public SpecMaterial Fields { get; }
}
