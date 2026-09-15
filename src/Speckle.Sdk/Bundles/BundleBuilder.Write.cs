using Speckle.Sdk.Bundles.Handles;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Bundles;

/// <summary>
/// Builder-side spelling of every handle write, for callers that hold the builder rather than chaining off a handle.
/// Each member forwards to the handle that owns the behaviour — see there for the semantics.
/// </summary>
public sealed partial class BundleBuilder
{
  // ── properties ────────────────────────────────────────────────────────────────────────────────────────

  /// <inheritdoc cref="BundleObject.SetProperties"/>
  public BundleObject SetProperties(
    BundleObject obj,
    IReadOnlyDictionary<string, object?>? properties,
    string? name = null,
    string? speckleType = null,
    string? sourceType = null,
    string? units = null,
    string? typeKey = null,
    IEnumerable<KeyValuePair<string, object?>>? rootScalars = null
  ) => obj.SetProperties(properties, name, speckleType, sourceType, units, typeKey, rootScalars);

  /// <inheritdoc cref="BundleObject.PropertiesWritten"/>
  public bool PropertiesWritten(BundleObject obj) => obj.PropertiesWritten;

  /// <inheritdoc cref="BundleObject.Assembly"/>
  public BundleObject? AssemblyOf(BundleObject obj) => obj.Assembly;

  /// <inheritdoc cref="BundleObject.Geometries"/>
  public IReadOnlyList<BundleGeometry> GeometriesOf(BundleObject obj) => obj.Geometries;

  // ── object geometry ───────────────────────────────────────────────────────────────────────────────────

  /// <inheritdoc cref="BundleObject.AddGeometry"/>
  public BundleGeometry AddGeometry(BundleObject obj, Base geometry, string? geometryKey = null) =>
    obj.AddGeometry(geometry, geometryKey);

  /// <inheritdoc cref="BundleObject.AddRawGeometry"/>
  public BundleGeometry AddRawGeometry(BundleObject obj, byte[] content, string type, string? geometryKey = null) =>
    obj.AddRawGeometry(content, type, geometryKey);

  /// <inheritdoc cref="BundleObject.AddCenterline"/>
  public BundleGeometry AddCenterline(BundleObject obj, Base curve, string? geometryKey = null) =>
    obj.AddCenterline(curve, geometryKey);

  /// <inheritdoc cref="BundleObject.Place"/>
  public BundleInstance Place(
    BundleObject obj,
    BundleDefinition definition,
    IReadOnlyList<double> transform,
    string? units = null,
    string? key = null
  ) => obj.Place(definition, transform, units, key);

  // ── definition geometry ───────────────────────────────────────────────────────────────────────────────

  /// <inheritdoc cref="BundleDefinition.AddGeometry"/>
  public BundleGeometry AddGeometry(
    BundleDefinition definition,
    Base geometry,
    string? geometryKey = null,
    int? memberOrd = null
  ) => definition.AddGeometry(geometry, geometryKey, memberOrd);

  /// <inheritdoc cref="BundleDefinition.AddRawGeometry"/>
  public BundleGeometry AddRawGeometry(
    BundleDefinition definition,
    byte[] content,
    string type,
    string? geometryKey = null,
    int? memberOrd = null
  ) => definition.AddRawGeometry(content, type, geometryKey, memberOrd);

  /// <inheritdoc cref="BundleDefinition.PlaceNested"/>
  public BundleInstance PlaceNested(
    BundleDefinition definition,
    BundleDefinition nested,
    IReadOnlyList<double> transform,
    string? units,
    string? key = null
  ) => definition.PlaceNested(nested, transform, units, key);

  /// <inheritdoc cref="BundleDefinition.AddMember"/>
  public IReadOnlyList<BundleGeometry> AddMember(
    BundleDefinition definition,
    BundleObject member,
    IEnumerable<Base> geometry,
    int? memberOrd = null
  ) => definition.AddMember(member, geometry, memberOrd);

  /// <inheritdoc cref="BundleDefinition.AddMemberRawGeometry"/>
  public BundleGeometry AddMemberRawGeometry(
    BundleDefinition definition,
    BundleObject member,
    byte[] content,
    string type,
    int memberOrd
  ) => definition.AddMemberRawGeometry(member, content, type, memberOrd);

  /// <inheritdoc cref="BundleDefinition.AddMemberPlacement"/>
  public BundleInstance AddMemberPlacement(
    BundleDefinition definition,
    BundleObject member,
    BundleDefinition nested,
    IReadOnlyList<double> transform,
    string? units = null,
    int? memberOrd = null
  ) => definition.AddMemberPlacement(member, nested, transform, units, memberOrd);

  /// <inheritdoc cref="BundleDefinition.AddExistingGeometry"/>
  public void AddExistingGeometry(BundleDefinition definition, BundleGeometry geometry, int? memberOrd = null) =>
    definition.AddExistingGeometry(geometry, memberOrd);

  /// <inheritdoc cref="BundleDefinition.NextMemberOrdinal"/>
  public int NextMemberOrdinal(BundleDefinition definition) => definition.NextMemberOrdinal();

  // ── geometry appearance ───────────────────────────────────────────────────────────────────────────────

  /// <inheritdoc cref="BundleGeometry.Material"/>
  public void HasMaterial(BundleGeometry geometry, BundleMaterial material) => geometry.Material = material;

  /// <inheritdoc cref="BundleGeometry.Color"/>
  public void HasColor(BundleGeometry geometry, BundleColor color) => geometry.Color = color;

  // ── object → node ─────────────────────────────────────────────────────────────────────────────────────

  /// <inheritdoc cref="BundleObject.Collection"/>
  public void InCollection(BundleObject obj, BundleContainer collection) => obj.Collection = collection;

  /// <inheritdoc cref="BundleObject.Model"/>
  public void InModel(BundleObject obj, BundleContainer model) => obj.Model = model;

  /// <inheritdoc cref="BundleObject.System"/>
  public void InSystem(BundleObject obj, BundleContainer system) => obj.System = system;

  /// <inheritdoc cref="BundleObject.Level"/>
  public void OnLevel(BundleObject obj, BundleLevel level) => obj.Level = level;

  /// <inheritdoc cref="BundleObject.Material"/>
  public void ObjectHasMaterial(BundleObject obj, BundleMaterial material) => obj.Material = material;

  /// <inheritdoc cref="BundleObject.Color"/>
  public void ObjectHasColor(BundleObject obj, BundleColor color) => obj.Color = color;

  /// <inheritdoc cref="BundleObject.AddToGroup"/>
  public void InGroup(BundleObject obj, BundleContainer group, int ord = 0) => obj.AddToGroup(group, ord);

  /// <inheritdoc cref="BundleContainer.Material"/>
  public void NodeHasMaterial(BundleContainer node, BundleMaterial material) => node.Material = material;

  /// <inheritdoc cref="BundleContainer.Color"/>
  public void NodeHasColor(BundleContainer node, BundleColor color) => node.Color = color;

  // ── object → object ───────────────────────────────────────────────────────────────────────────────────

  /// <inheritdoc cref="BundleObject.AddChild"/>
  public void Subelement(BundleObject parent, BundleObject child, int? ord = null) => parent.AddChild(child, ord);

  /// <inheritdoc cref="BundleObject.AddAssemblyMember"/>
  public void InAssembly(BundleObject assembly, BundleObject member, int? ord = null) =>
    assembly.AddAssemblyMember(member, ord);

  /// <inheritdoc cref="BundleObject.Host"/>
  public void HostedOn(BundleObject obj, BundleObject host) => obj.Host = host;

  /// <inheritdoc cref="BundleObject.Room"/>
  public void InRoom(BundleObject obj, BundleObject room) => obj.Room = room;

  /// <inheritdoc cref="BundleObject.ConnectTo"/>
  public void ConnectsTo(BundleObject obj, BundleObject other, int scope = 0) => obj.ConnectTo(other, scope);

  /// <inheritdoc cref="BundleObject.Bounds"/>
  public void Bounds(BundleObject obj, BundleObject room, int ord = 0) => obj.Bounds(room, ord);
}
