using System.Globalization;
using Speckle.Sdk.Pipelines.Receive.Artifacts;

namespace Speckle.Sdk.Bundles;

/// <summary>
/// One grouping tier of the bundle's default scene view — the hierarchy the viewer shows. A tier groups objects
/// either by a relation to a node (<c>IN_MODEL</c> → Model container, <c>ON_LEVEL</c> → level, <c>IN_COLLECTION</c> → layer …)
/// or by the value of a property path (Revit: <c>category</c>, <c>family</c>). Producers choose; Revit-shaped bundles
/// are mostly EAV tiers over flat containers, Rhino-shaped ones are nested <c>IN_COLLECTION</c> containers.
/// </summary>
public sealed record ModelSceneViewTier
{
  internal ModelSceneViewTier(SceneViewTier tier)
  {
    IsRelation = tier.Source == "rel";
    Relation =
      IsRelation && int.TryParse(tier.Ref, NumberStyles.Integer, CultureInfo.InvariantCulture, out int r) ? r : null;
    PropertyPath = IsRelation ? null : tier.Ref;
  }

  /// <summary>True for a relation-sourced tier, false for a property-value tier.</summary>
  public bool IsRelation { get; }

  /// <summary>The object→node relation number (a <c>Speckle.Bundle.Spec.Rel</c> value) for relation tiers.</summary>
  public int? Relation { get; }

  /// <summary>The property path grouped on, for property-value tiers.</summary>
  public string? PropertyPath { get; }

  public override string ToString() => IsRelation ? $"rel {Relation}" : $"eav {PropertyPath}";
}

/// <summary>One segment of an object's position in the default scene view: the group name and, for relation tiers,
/// the node that group is (null for property-value groups, which have no node).</summary>
public sealed record ModelSceneViewSegment(string Name, ModelNode? Node);
