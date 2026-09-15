namespace Speckle.Sdk.Bundles.Handles;

/// <summary>A node: carries the dense K the pipeline assigned. Ks are scoped to the bundle that minted them.</summary>
public abstract class BundleNode
{
  private protected BundleNode(BundleBuilder builder, int k)
  {
    Builder = builder;
    K = k;
  }

  internal BundleBuilder Builder { get; }

  /// <summary>Dense node index inside this bundle.</summary>
  public int K { get; }
}
