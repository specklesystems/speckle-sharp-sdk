namespace Speckle.Sdk.Bundles.Handles;

/// <summary>A node: carries the dense K the pipeline assigned. Ks are scoped to the bundle that minted them.</summary>
public abstract class BundleNode
{
  private protected BundleNode(int k)
  {
    K = k;
  }

  /// <summary>Dense node index inside this bundle.</summary>
  public int K { get; }
}
