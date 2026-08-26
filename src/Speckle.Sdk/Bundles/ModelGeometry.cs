using Speckle.Objects.Utils;
using Speckle.Sdk.Pipelines.Receive.Artifacts;

namespace Speckle.Sdk.Bundles;

/// <summary>How a geometry relates to the object it was resolved for.</summary>
public enum GeometryRole
{
  /// <summary>Render mesh (<c>DISPLAY</c> edge, or a definition member reached through a placement).</summary>
  Display,

  /// <summary>Authoritative host solid (<c>SOLID</c> edge) — a raw host-format blob, e.g. a 3dm brep.</summary>
  Solid,
}

/// <summary>
/// One geometry of a <see cref="ModelObject"/>: the raw bundle blob plus how it was reached. Decoding is on demand —
/// holding a <see cref="ModelGeometry"/> costs the bytes only.
/// </summary>
public sealed class ModelGeometry
{
  private readonly Model _model;
  private readonly int? _instanceK;

  internal ModelGeometry(
    Model model,
    int k,
    ArtefactGeometry geometry,
    GeometryRole role,
    int ord,
    IReadOnlyList<double>? transform,
    int? instanceK
  )
  {
    _model = model;
    _instanceK = instanceK;
    K = k;
    Content = geometry.Content;
    Type = geometry.Type;
    IsSgeo = geometry.IsSgeo;
    Role = role;
    Ord = ord;
    Transform = transform;
  }

  /// <summary>Dense geometry index — the key into <see cref="Model.Geometries"/>. Distinct id space from object Ks.</summary>
  public int K { get; }

  /// <summary>Raw bytes as stored in the bundle: SGEO when <see cref="IsSgeo"/>, else a host-format blob.</summary>
  public ReadOnlyMemory<byte> Content { get; }

  /// <summary>Producer's type tag (<c>"sgeo"</c>, <c>"3dm"</c>, …).</summary>
  public string Type { get; }

  /// <summary>True when <see cref="Content"/> is Speckle's SGEO mesh encoding and <see cref="DecodeMesh"/> applies.</summary>
  public bool IsSgeo { get; }

  public GeometryRole Role { get; }

  /// <summary>Draw order within the object (the edge ordinal).</summary>
  public int Ord { get; }

  /// <summary>
  /// Placement transform to apply to the geometry, as the 16 values stored on the INSTANCE node (nested placements
  /// composed outer→inner); <see langword="null"/> when the object references the geometry directly. Definition
  /// geometry is shared between placements — always apply this rather than mutating the decoded mesh.
  /// </summary>
  public IReadOnlyList<double>? Transform { get; }

  /// <summary>The INSTANCE node this geometry was reached through; null for directly referenced geometry.</summary>
  public ModelInstance? Placement => _model.NodeOrNull(_instanceK) as ModelInstance;

  /// <summary>
  /// Render material for this geometry: a material painted on the placement (<c>HAS_MATERIAL</c> with instance src)
  /// wins over the geometry's own (<c>HAS_MATERIAL</c>). Null when neither is set — fall back to
  /// <see cref="ModelObject.Material"/>, then the container's <see cref="ModelNode.Material"/>.
  /// </summary>
  public ModelMaterial? Material
  {
    get
    {
      var rels = _model.Bundle.Relations;
      if (_instanceK is int ik && rels.MaterialByInstance.TryGetValue(ik, out int im))
      {
        return _model.NodeOrNull(im) as ModelMaterial;
      }
      return rels.MaterialByGeometry.TryGetValue(K, out int m) ? _model.NodeOrNull(m) as ModelMaterial : null;
    }
  }

  /// <summary>Colour painted on this geometry (<c>HAS_COLOR</c>). Null when unset — fall back to <see cref="ModelObject.Color"/>.</summary>
  public ModelColor? Color =>
    _model.Bundle.Relations.ColorByGeometry.TryGetValue(K, out int c) ? _model.NodeOrNull(c) as ModelColor : null;

  /// <summary>Decodes an SGEO blob into vertices/faces/colours; <see langword="null"/> for non-SGEO or corrupt blobs.</summary>
  public SgeoMesh? DecodeMesh() => IsSgeo && SgeoDecoder.TryDecodeMesh(Content.Span, out var mesh) ? mesh : null;

  public override string ToString() => $"geometry {K} ({Type}, {Role}{(Transform is null ? "" : ", placed")})";
}
