using SpecColor = Speckle.Bundle.Spec.Color;

namespace Speckle.Sdk.Bundles.Handles;

public sealed class BundleColor : BundleNode
{
  internal BundleColor(int k, SpecColor fields)
    : base(k)
  {
    Fields = fields;
  }

  /// <summary>The COLOR row this handle points at.</summary>
  public SpecColor Fields { get; }
}
