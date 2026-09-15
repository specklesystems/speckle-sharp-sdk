using SpecContainer = Speckle.Bundle.Spec.Container;

namespace Speckle.Sdk.Bundles.Handles;

/// <summary>A CONTAINER node (spec node kind 7): layer, category, folder, federated model, group, MEP system …
/// (its subtype). Objects reference it through a rel — <c>IN_COLLECTION</c> for the authored scene tree,
/// <c>IN_MODEL</c>, <c>IN_GROUP</c>, <c>IN_SYSTEM</c> for the other axes.</summary>
public sealed class BundleContainer : BundleNode
{
  private BundleMaterial? _material;
  private BundleColor? _color;

  internal BundleContainer(BundleBuilder builder, int k, string key, SpecContainer fields, BundleContainer? parent)
    : base(builder, k)
  {
    Key = key;
    Fields = fields;
    Parent = parent;
  }

  /// <summary>The intern key this container was added under.</summary>
  public string Key { get; }

  /// <summary>The CONTAINER row this handle points at.</summary>
  public SpecContainer Fields { get; }

  /// <summary>The parent handle behind <see cref="SpecContainer.DefRef"/>.</summary>
  public BundleContainer? Parent { get; }

  /// <summary>The container's authored render material (<c>NODE_HAS_MATERIAL</c>): a layer or tag material. Weakest
  /// tier of the material ladder — geometry, then object, then this.</summary>
  public BundleMaterial? Material
  {
    get => _material;
    set
    {
      if (value is null || ReferenceEquals(_material, value))
      {
        return;
      }
      _material = value;
      Builder.Pipeline.NodeHasMaterial(K, value.K);
    }
  }

  /// <summary>The container's display colour (<c>NODE_HAS_COLOR</c>): the ByLayer default that applies when nothing
  /// more specific does.</summary>
  public BundleColor? Color
  {
    get => _color;
    set
    {
      if (value is null || ReferenceEquals(_color, value))
      {
        return;
      }
      _color = value;
      Builder.Pipeline.NodeHasColor(K, value.K);
    }
  }

  public override string ToString() => $"{Fields.Subtype} '{Fields.Name}'";
}
