namespace Speckle.Sdk.Bundles.Handles;

/// <summary>One geometry (SGEO mesh or raw host blob). Painting it is the strongest tier of the appearance ladder:
/// geometry, then object, then container.</summary>
public sealed class BundleGeometry
{
  private readonly BundleBuilder _builder;
  private BundleMaterial? _material;
  private BundleColor? _color;

  internal BundleGeometry(BundleBuilder builder, int k, int ord)
  {
    _builder = builder;
    K = k;
    Ord = ord;
  }

  /// <summary>Dense geometry index inside this bundle.</summary>
  public int K { get; }

  /// <summary>Draw order within its owner.</summary>
  public int Ord { get; }

  /// <summary>Geometry-plane material (<c>HAS_MATERIAL</c>): the intrinsic render material.</summary>
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
      _builder.Pipeline.HasMaterial(K, value.K);
    }
  }

  /// <summary>Geometry-plane colour (<c>HAS_COLOR</c>).</summary>
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
      _builder.Pipeline.HasColor(K, value.K);
    }
  }

  public override string ToString() => $"geometry {K} (ord {Ord})";
}
