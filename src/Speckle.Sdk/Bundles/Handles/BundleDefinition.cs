using SpecDefinition = Speckle.Bundle.Spec.Definition;

namespace Speckle.Sdk.Bundles.Handles;

/// <summary>A DEFINITION node (block / family symbol): geometry shared by every placement, plus nested placements
/// and member objects — all written through <see cref="BundleBuilder"/>.</summary>
public sealed class BundleDefinition : BundleNode
{
  internal BundleDefinition(int k, string key, SpecDefinition fields)
    : base(k)
  {
    Key = key;
    Fields = fields;
  }

  /// <summary>The intern key this definition was added under; geometry keys are derived from it.</summary>
  public string Key { get; }

  /// <summary>The DEFINITION row this handle points at.</summary>
  public SpecDefinition Fields { get; }

  // One authored sequence numbers geometry and members alike, so both counters advance together.
  internal int GeometryOrd;
  internal int MemberOrd;
}
