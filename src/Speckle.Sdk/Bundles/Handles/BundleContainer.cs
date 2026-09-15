using SpecContainer = Speckle.Bundle.Spec.Container;

namespace Speckle.Sdk.Bundles.Handles;

/// <summary>A CONTAINER node (spec node kind 7): layer, category, folder, federated model, group, MEP system …
/// (its subtype). Objects reference it through a rel — <c>IN_COLLECTION</c> for the authored scene tree,
/// <c>IN_MODEL</c>, <c>IN_GROUP</c>, <c>IN_SYSTEM</c> for the other axes.</summary>
public sealed class BundleContainer : BundleNode
{
  internal BundleContainer(int k, SpecContainer fields, BundleContainer? parent)
    : base(k)
  {
    Fields = fields;
    Parent = parent;
  }

  /// <summary>The CONTAINER row this handle points at.</summary>
  public SpecContainer Fields { get; }

  /// <summary>The parent handle behind <see cref="SpecContainer.DefRef"/>.</summary>
  public BundleContainer? Parent { get; }

  internal BundleMaterial? NodeMaterial;
  internal BundleColor? NodeColor;

  public override string ToString() => $"{Fields.Subtype} '{Fields.Name}'";
}
