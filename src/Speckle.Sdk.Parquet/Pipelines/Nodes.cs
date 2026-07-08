using Speckle.Sdk.Pipelines.Send.Artifacts;
using SpecKind = Speckle.Bundle.Spec.NodeKind;
using SpecRel = Speckle.Bundle.Spec.Rel;

namespace Speckle.Sdk.Pipelines;

/// <summary>Edge kinds in the envelope <c>relations</c> table — a thin PascalCase facade over the
/// generated <c>Speckle.Bundle.Spec.Rel</c> enum so connectors keep an idiomatic C# vocabulary while the
/// VALUES, the <c>rel_types</c> catalog, and the table schemas all come from the single source of truth
/// (the <c>speckle-bundle-spec</c> sibling repo). Add a new rel in the spec and regenerate; retired rels
/// are simply absent here. See <see cref="EnvelopeWriter.WriteCatalog"/>.</summary>
public static class RelKind
{
  public const byte Display = (byte)SpecRel.DISPLAY;
  public const byte Solid = (byte)SpecRel.SOLID;
  public const byte Subelement = (byte)SpecRel.SUBELEMENT;
  public const byte Defines = (byte)SpecRel.DEFINES;
  public const byte HasMaterial = (byte)SpecRel.HAS_MATERIAL;
  public const byte HasColor = (byte)SpecRel.HAS_COLOR;
  public const byte OnLevel = (byte)SpecRel.ON_LEVEL;
  public const byte DisplayInstance = (byte)SpecRel.DISPLAY_INSTANCE;
  public const byte DefinesInstance = (byte)SpecRel.DEFINES_INSTANCE;
  public const byte InCollection = (byte)SpecRel.IN_COLLECTION;
  public const byte InModel = (byte)SpecRel.IN_MODEL;
  public const byte InRoom = (byte)SpecRel.IN_ROOM;
  public const byte InSystem = (byte)SpecRel.IN_SYSTEM;
  public const byte ConnectsTo = (byte)SpecRel.CONNECTS_TO;
  public const byte Bounds = (byte)SpecRel.BOUNDS;
}

/// <summary>Value-node kinds in the envelope <c>nodes</c> table — a thin facade over the generated
/// <c>Speckle.Bundle.Spec.NodeKind</c> enum. COLLECTION folded into CONTAINER (v5): a scene-tree
/// collection is a CONTAINER whose <c>subtype</c> carries its tag; the inbound rel (<c>IN_COLLECTION</c>
/// vs <c>IN_MODEL</c>/<c>IN_SYSTEM</c>/…) disambiguates the grouping axis.</summary>
public static class NodeKind
{
  public const byte Definition = (byte)SpecKind.DEFINITION;
  public const byte Instance = (byte)SpecKind.INSTANCE;
  public const byte Material = (byte)SpecKind.MATERIAL;
  public const byte Color = (byte)SpecKind.COLOR;
  public const byte Level = (byte)SpecKind.LEVEL;
  public const byte Container = (byte)SpecKind.CONTAINER;
}

/// <summary>The store a <see cref="SceneViewKey"/> reads from: a relation walk or an eav group-by.</summary>
public enum ProjectionSource
{
  /// <summary>Relation walk — <see cref="SceneViewKey.Ref"/> is a <see cref="RelKind"/> code (joins rel_types).</summary>
  Rel,

  /// <summary>EAV group-by — <see cref="SceneViewKey.Ref"/> is the eav attribute key.</summary>
  Eav,
}
