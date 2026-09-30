using SpecLevel = Speckle.Bundle.Spec.Level;

namespace Speckle.Sdk.Bundles.Handles;

public sealed class BundleLevel : BundleNode
{
  internal BundleLevel(BundleBuilder builder, int k, string key, SpecLevel fields)
    : base(builder, k)
  {
    Key = key;
    Fields = fields;
  }

  /// <summary>The intern key this level was added under.</summary>
  public string Key { get; }

  /// <summary>The LEVEL row this handle points at.</summary>
  public SpecLevel Fields { get; }
}
