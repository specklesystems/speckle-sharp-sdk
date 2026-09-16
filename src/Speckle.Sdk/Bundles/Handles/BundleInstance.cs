namespace Speckle.Sdk.Bundles.Handles;

/// <summary>An INSTANCE node: one placement of a <see cref="BundleDefinition"/>.</summary>
public sealed class BundleInstance : BundleNode
{
  internal BundleInstance(BundleBuilder builder, int k, BundleDefinition definition)
    : base(builder, k)
  {
    Definition = definition;
  }

  public BundleDefinition Definition { get; }
}
