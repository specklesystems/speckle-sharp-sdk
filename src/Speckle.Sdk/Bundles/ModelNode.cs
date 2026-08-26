using System.Globalization;
using Speckle.Bundle.Spec;
using Speckle.Sdk.Pipelines.Receive.Artifacts;

namespace Speckle.Sdk.Bundles;

/// <summary>
/// A non-object entity of a received <see cref="Model"/>: a <see cref="ModelLevel"/>, <see cref="ModelMaterial"/>,
/// <see cref="ModelColor"/>, <see cref="ModelDefinition"/>, <see cref="ModelInstance"/> or <see cref="ModelContainer"/>.
/// Nodes have no <c>applicationId</c>; they are identified by their dense <see cref="K"/> within this model only.
/// Kinds this SDK doesn't know (a newer bundle spec) come back as the base <see cref="ModelNode"/>.
/// </summary>
public class ModelNode
{
  private protected readonly Model _model;
  private protected readonly ArtefactNode _node;

  internal ModelNode(Model model, int k, ArtefactNode node)
  {
    _model = model;
    K = k;
    _node = node;
  }

  /// <summary>Dense node index — the key into <see cref="ArtefactBundle.Nodes"/>. Distinct id space from object and geometry Ks.</summary>
  public int K { get; }

  /// <summary>Node kind, straight from the bundle spec.</summary>
  public NodeKind Kind => (NodeKind)_node.Kind;

  public string? Name => _node.Name;

  public string? Units => _node.Units;

  /// <summary>Material painted on this node (<c>NODE_HAS_MATERIAL</c>).</summary>
  public virtual ModelMaterial? Material =>
    _model.Bundle.Relations.MaterialByNode.TryGetValue(K, out int m) ? _model.Node(m) as ModelMaterial : null;

  /// <summary>Colour painted on this node (<c>NODE_HAS_COLOR</c>).</summary>
  public ModelColor? Color =>
    _model.Bundle.Relations.ColorByNode.TryGetValue(K, out int c) ? _model.Node(c) as ModelColor : null;

  public override string ToString() => Name is null ? $"{Kind} {K}" : $"{Kind} {K} '{Name}'";

  internal static ModelNode Create(Model model, int k, ArtefactNode node) =>
    (NodeKind)node.Kind switch
    {
      NodeKind.LEVEL => new ModelLevel(model, k, node),
      NodeKind.MATERIAL => new ModelMaterial(model, k, node),
      NodeKind.COLOR => new ModelColor(model, k, node),
      NodeKind.DEFINITION => new ModelDefinition(model, k, node),
      NodeKind.INSTANCE => new ModelInstance(model, k, node),
      NodeKind.CONTAINER => new ModelContainer(model, k, node),
      _ => new ModelNode(model, k, node),
    };
}

/// <summary>A storey (<c>LEVEL</c>). Target of <see cref="ModelObject.Level"/>.</summary>
public sealed class ModelLevel : ModelNode
{
  internal ModelLevel(Model model, int k, ArtefactNode node)
    : base(model, k, node) { }

  public double? Elevation => _node.Elevation;

  /// <summary>Objects on this level (<c>ON_LEVEL</c> reversed).</summary>
  public IReadOnlyList<ModelObject> Objects => _model.ObjectsOnLevel(K);
}

/// <summary>A render material (<c>MATERIAL</c>): PBR parameters, deduplicated per bundle.</summary>
public sealed class ModelMaterial : ModelNode
{
  internal ModelMaterial(Model model, int k, ArtefactNode node)
    : base(model, k, node) { }

  /// <summary>Diffuse colour, ARGB.</summary>
  public int? Argb => _node.Argb;
  public double? Opacity => _node.Opacity;
  public double? Metalness => _node.Metalness;
  public double? Roughness => _node.Roughness;

  /// <summary>Emissive colour, ARGB.</summary>
  public int? Emissive => _node.Emissive;
  public double? Ior => _node.Ior;
}

/// <summary>A flat colour (<c>COLOR</c>), deduplicated per bundle.</summary>
public sealed class ModelColor : ModelNode
{
  internal ModelColor(Model model, int k, ArtefactNode node)
    : base(model, k, node) { }

  public int Argb => _node.Argb ?? 0;
}

/// <summary>A block / family definition (<c>DEFINITION</c>): shared geometry placed by <see cref="ModelInstance"/>s.</summary>
public sealed class ModelDefinition : ModelNode
{
  internal ModelDefinition(Model model, int k, ArtefactNode node)
    : base(model, k, node) { }

  /// <summary>Every placement of this definition.</summary>
  public IReadOnlyList<ModelInstance> Placements => _model.PlacementsOfDefinition(K);

  /// <summary>Member objects (<c>DEFINES_MEMBER</c>) — the property carriers behind the definition's geometry.</summary>
  public IReadOnlyList<ModelObject> Members => _model.MembersOfDefinition(K);
}

/// <summary>One placement of a <see cref="ModelDefinition"/> (<c>INSTANCE</c>): a transform plus the definition it places.</summary>
public sealed class ModelInstance : ModelNode
{
  private readonly Lazy<IReadOnlyList<double>?> _transform;

  internal ModelInstance(Model model, int k, ArtefactNode node)
    : base(model, k, node)
  {
    _transform = new(() => ParseTransform(node.Transform));
  }

  /// <summary>Placement transform: 16 values, row-major as stored in the bundle.</summary>
  public IReadOnlyList<double>? Transform => _transform.Value;

  /// <summary>The definition this placement instantiates.</summary>
  public ModelDefinition? Definition => _model.NodeOrNull(_node.DefRef) as ModelDefinition;

  /// <summary>Material: <c>NODE_HAS_MATERIAL</c>, else the legacy placement-painted <c>HAS_MATERIAL</c> (instance src).</summary>
  public override ModelMaterial? Material =>
    base.Material
    ?? (
      _model.Bundle.Relations.MaterialByInstance.TryGetValue(K, out int im) ? _model.Node(im) as ModelMaterial : null
    );

  internal static IReadOnlyList<double>? ParseTransform(string? csv)
  {
    if (csv is null || csv.Length == 0)
    {
      return null;
    }
    var parts = csv.Split(',');
    if (parts.Length != 16)
    {
      return null;
    }
    var m = new double[16];
    for (int i = 0; i < 16; i++)
    {
      if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out m[i]))
      {
        return null;
      }
    }
    return m;
  }
}

/// <summary>A scene-tree container (<c>CONTAINER</c>): layer, category, folder, model, MEP system, group …</summary>
public sealed class ModelContainer : ModelNode
{
  internal ModelContainer(Model model, int k, ArtefactNode node)
    : base(model, k, node) { }

  /// <summary>Container flavour as the producer wrote it: <c>Collection</c>, <c>Layer</c>, <c>Folder</c>, <c>Model</c>,
  /// <c>MEP System</c>, <c>Network</c>, <c>Group</c> … Null on bundles written before the column existed.</summary>
  public string? Subtype => _node.Subtype;

  /// <summary>The enclosing container. Null at the root.</summary>
  public ModelContainer? Parent => _model.NodeOrNull(_node.DefRef) as ModelContainer;

  /// <summary>Names from the outermost container down to this one.</summary>
  public IReadOnlyList<string> Path => SceneViewResolver.NodeAncestry(_model.Bundle.Nodes, K);

  /// <summary>Objects directly in this container (<c>IN_COLLECTION</c>).</summary>
  public IReadOnlyList<ModelObject> Objects => _model.ObjectsInCollection(K);

  /// <summary>Child containers.</summary>
  public IReadOnlyList<ModelContainer> Children => _model.ChildContainers(K);

  /// <summary>Legacy container colour stamped directly on the row (pre-<c>NODE_HAS_COLOR</c> bundles), ARGB.</summary>
  public int? Argb => _node.Argb;
}
