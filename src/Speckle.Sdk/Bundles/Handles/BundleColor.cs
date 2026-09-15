using SpecColor = Speckle.Bundle.Spec.Color;

namespace Speckle.Sdk.Bundles.Handles;

public sealed class BundleColor : BundleNode
{
  internal BundleColor(BundleBuilder builder, int k, SpecColor fields)
    : base(builder, k)
  {
    Fields = fields;
  }

  /// <summary>The COLOR row this handle points at.</summary>
  public SpecColor Fields { get; }
}
