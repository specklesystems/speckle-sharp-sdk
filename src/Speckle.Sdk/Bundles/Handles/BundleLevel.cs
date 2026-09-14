using SpecLevel = Speckle.Bundle.Spec.Level;

namespace Speckle.Sdk.Bundles.Handles;

public sealed class BundleLevel : BundleNode
{
  internal BundleLevel(int k, SpecLevel fields)
    : base(k)
  {
    Fields = fields;
  }

  /// <summary>The LEVEL row this handle points at.</summary>
  public SpecLevel Fields { get; }
}
