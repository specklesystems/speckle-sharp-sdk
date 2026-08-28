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
    ModelObject owner,
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
    Owner = owner;
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

  /// <summary>The object this geometry was resolved for.</summary>
  public ModelObject Owner { get; }

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

  /// <summary>Material on the geometry plane (<c>HAS_MATERIAL</c>) — the intrinsic render material. Null when unset;
  /// <see cref="EffectiveMaterial"/> applies the fallback chain.</summary>
  public ModelMaterial? Material =>
    _model.Bundle.Relations.MaterialByGeometry.TryGetValue(K, out int m) ? _model.NodeOrNull(m) as ModelMaterial : null;

  /// <summary>Colour on the geometry plane (<c>HAS_COLOR</c>). Null when unset; <see cref="EffectiveColor"/> applies
  /// the override chain.</summary>
  public ModelColor? Color =>
    _model.Bundle.Relations.ColorByGeometry.TryGetValue(K, out int c) ? _model.NodeOrNull(c) as ModelColor : null;

  /// <summary>The material to render with, per the spec's precedence: the geometry's own, else the object's
  /// (<c>OBJECT_HAS_MATERIAL</c> fills where geometry has none), else the nearest container's
  /// (<c>NODE_HAS_MATERIAL</c>, walking up). Null when nothing in the chain is set.</summary>
  public ModelMaterial? EffectiveMaterial => Material ?? Owner.Material ?? Owner.ContainerMaterial;

  /// <summary>The colour to display with, per the spec's precedence: the object's (<c>OBJECT_HAS_COLOR</c> overrides —
  /// the inverse of material), else the geometry's own, else the nearest container's (<c>NODE_HAS_COLOR</c>). Null
  /// when nothing in the chain is set; render from <see cref="EffectiveMaterial"/> then.</summary>
  public ModelColor? EffectiveColor => Owner.Color ?? Color ?? Owner.ContainerColor;

  /// <summary>Decodes an SGEO blob into vertices/faces/colours; <see langword="null"/> for non-SGEO or corrupt blobs.</summary>
  public SgeoMesh? DecodeMesh() => IsSgeo && SgeoDecoder.TryDecodeMesh(Content.Span, out var mesh) ? mesh : null;

  public override string ToString() => $"geometry {K} ({Type}, {Role}{(Transform is null ? "" : ", placed")})";
}
