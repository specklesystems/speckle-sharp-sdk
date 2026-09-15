using Speckle.Sdk.Models;
using SpecDefinition = Speckle.Bundle.Spec.Definition;

namespace Speckle.Sdk.Bundles.Handles;

/// <summary>A DEFINITION node (block / family symbol): geometry shared by every placement, plus nested placements
/// and member objects.</summary>
public sealed class BundleDefinition : BundleNode
{
  // One authored sequence numbers geometry and members alike, so both counters advance together.
  private int _geometryOrd;
  private int _memberOrd;

  internal BundleDefinition(BundleBuilder builder, int k, string key, SpecDefinition fields)
    : base(builder, k)
  {
    Key = key;
    Fields = fields;
  }

  /// <summary>The intern key this definition was added under; geometry keys are derived from it.</summary>
  public string Key { get; }

  /// <summary>The DEFINITION row this handle points at.</summary>
  public SpecDefinition Fields { get; }

  /// <summary>Definition geometry (<c>DEFINES</c>). <paramref name="memberOrd"/> groups several geometries under one
  /// member (a member's solid and its display meshes share an ordinal); null = next ordinal.</summary>
  public BundleGeometry AddGeometry(Base geometry, string? geometryKey = null, int? memberOrd = null)
  {
    int ord = memberOrd ?? _geometryOrd++;
    int gK = Builder.Pipeline.AddGeometry(geometryKey ?? $"{Key}:g{ord}", geometry);
    Builder.Pipeline.Defines(K, gK, ord);
    return new BundleGeometry(Builder, gK, ord);
  }

  /// <summary>Raw host-format definition geometry (a 3dm brep), kept losslessly next to its display meshes.</summary>
  public BundleGeometry AddRawGeometry(byte[] content, string type, string? geometryKey = null, int? memberOrd = null)
  {
    int ord = memberOrd ?? _geometryOrd++;
    int gK = Builder.Pipeline.AddRawGeometry(geometryKey ?? $"{Key}:raw{ord}", content, type);
    Builder.Pipeline.Defines(K, gK, ord);
    return new BundleGeometry(Builder, gK, ord);
  }

  /// <summary>A nested placement inside this definition (<c>DEFINES_INSTANCE</c>): a block that contains a block.</summary>
  public BundleInstance PlaceNested(
    BundleDefinition nested,
    IReadOnlyList<double> transform,
    string? units,
    string? key = null
  )
  {
    int ord = _geometryOrd++;
    int instK = Builder.Pipeline.AddInstance(key ?? $"{Key}:inst{ord}", nested.K, transform, units);
    Builder.Pipeline.DefinesInstance(K, instK, ord);
    return new BundleInstance(Builder, instK, nested);
  }

  /// <summary>
  /// An authored member of this definition that owns its own geometry (Rhino / AutoCAD block contents): the member's
  /// object row carries its layer and properties, its geometry renders ONLY through placements of the definition —
  /// so it gets <c>DEFINES</c> + <c>DEFINES_MEMBER</c> on one member ordinal and no <c>DISPLAY</c> edge of its own.
  /// Returns the geometry handles (paint them with <see cref="BundleGeometry.Material"/> as usual).
  /// </summary>
  public IReadOnlyList<BundleGeometry> AddMember(BundleObject member, IEnumerable<Base> geometry, int? memberOrd = null)
  {
    int ord = memberOrd ?? NextMemberOrdinal();
    Builder.Pipeline.DefinesMember(K, member.K, ord);
    var handles = new List<BundleGeometry>();
    int i = 0;
    foreach (var g in geometry)
    {
      string key = $"{member.ApplicationId}:g{i++}";
      int gK = Builder.Pipeline.AddGeometry(key, g);
      Builder.Pipeline.Defines(K, gK, ord);
      handles.Add(Builder.RegisterGeometry(key, new BundleGeometry(Builder, gK, ord)));
    }
    Bump(ord);
    return handles;
  }

  /// <summary>A member's raw host geometry (a 3dm solid) alongside its display meshes — same member ordinal, so a
  /// receiver can pick the solid over its shadow. Call after <see cref="AddMember"/> with the ordinal it returned
  /// via <see cref="BundleGeometry.Ord"/>.</summary>
  public BundleGeometry AddMemberRawGeometry(BundleObject member, byte[] content, string type, int memberOrd)
  {
    string key = $"{member.ApplicationId}:raw{memberOrd}";
    int gK = Builder.Pipeline.AddRawGeometry(key, content, type);
    Builder.Pipeline.Defines(K, gK, memberOrd);
    return Builder.RegisterGeometry(key, new BundleGeometry(Builder, gK, memberOrd));
  }

  /// <summary>A member that is itself a placement of another definition (a block inside a block): <c>INSTANCE</c> +
  /// <c>DEFINES_INSTANCE</c> + <c>DEFINES_MEMBER</c> + <c>PLACES</c> (member → its placement), no
  /// <c>DISPLAY_INSTANCE</c>.</summary>
  public BundleInstance AddMemberPlacement(
    BundleObject member,
    BundleDefinition nested,
    IReadOnlyList<double> transform,
    string? units = null,
    int? memberOrd = null
  )
  {
    int ord = memberOrd ?? NextMemberOrdinal();
    int instK = Builder.Pipeline.AddInstance(member.ApplicationId, nested.K, transform, units ?? Builder.Units);
    Builder.Pipeline.DefinesInstance(K, instK, ord);
    Builder.Pipeline.DefinesMember(K, member.K, ord);
    Builder.Pipeline.Places(member.K, instK);
    Bump(ord);
    return new BundleInstance(Builder, instK, nested);
  }

  /// <summary>References geometry already written elsewhere (Revit: a family's mesh added under an element, shared by
  /// the symbol) as this definition's geometry (<c>DEFINES</c>), without re-encoding.</summary>
  public void AddExistingGeometry(BundleGeometry geometry, int? memberOrd = null)
  {
    int ord = memberOrd ?? NextMemberOrdinal();
    Builder.Pipeline.Defines(K, geometry.K, ord);
    Bump(ord);
  }

  /// <summary>The next unused member ordinal. The (definition, ordinal) pair joins a member's object row to its
  /// geometry, which is what survives content-hash geometry dedup.</summary>
  public int NextMemberOrdinal() => Math.Max(_geometryOrd, _memberOrd);

  private void Bump(int ord)
  {
    _memberOrd = Math.Max(_memberOrd, ord + 1);
    _geometryOrd = Math.Max(_geometryOrd, ord + 1);
  }
}
