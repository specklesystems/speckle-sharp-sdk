using SpecColor = Speckle.Bundle.Spec.Color;
using SpecContainer = Speckle.Bundle.Spec.Container;
using SpecDefinition = Speckle.Bundle.Spec.Definition;
using SpecLevel = Speckle.Bundle.Spec.Level;
using SpecMaterial = Speckle.Bundle.Spec.Material;

namespace Speckle.Sdk.Pipelines.Receive.Artifacts;

/// <summary>
/// Projects a wide <see cref="ArtefactNode"/> row onto the per-kind record the spec declares for it. Columns the
/// spec marks mandatory are non-nullable on those records, so a NULL is a producer defect and fails loud here
/// instead of being quietly defaulted — read- and write-side defaults used to be invented independently.
/// </summary>
public static class NodeFields
{
  public static SpecMaterial Material(int k, ArtefactNode node) =>
    new(
      node.Name,
      Required(node.Argb, k, "argb"),
      Required(node.Opacity, k, "opacity"),
      Required(node.Metalness, k, "metalness"),
      Required(node.Roughness, k, "roughness"),
      node.Emissive,
      node.Ior
    );

  public static SpecColor Color(int k, ArtefactNode node) => new(Required(node.Argb, k, "argb"));

  public static SpecLevel Level(int k, ArtefactNode node) => new(node.Name, Required(node.Elevation, k, "elevation"));

  public static SpecContainer Container(ArtefactNode node) =>
    new(node.Name, node.DefRef, node.Subtype, node.GhTopology);

  public static SpecDefinition Definition(ArtefactNode node) => new(node.Name, node.DefRef);

  private static T Required<T>(T? value, int k, string column)
    where T : struct =>
    value
    ?? throw new InvalidOperationException(
      $"Node {k}: nodes.{column} is mandatory for this node kind in the bundle spec, but the row has NULL."
    );
}
