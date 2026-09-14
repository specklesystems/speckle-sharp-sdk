using Speckle.Sdk.Bundles.Handles;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Bundles;

/// <summary>
/// Everything that writes an edge or a row for an already-interned entity. The entities themselves carry no writer,
/// so this is the only place a bundle is mutated — and the only place that holds the per-entity ordinals and the
/// write-once state a single-valued relation needs.
/// </summary>
public sealed partial class BundleBuilder
{
  private readonly Dictionary<int, ObjectState> _objectState = new();
  private readonly Dictionary<int, DefinitionState> _definitionState = new();
  private readonly Dictionary<int, int> _nodeMaterial = new();
  private readonly Dictionary<int, int> _nodeColor = new();

  private sealed class ObjectState
  {
    public readonly List<BundleGeometry> Geometries = new();
    public int DisplayOrd;
    public int SolidOrd;
    public int CenterlineOrd;
    public int PlacementOrd;
    public int ChildOrd;
    public int AssemblyMemberOrd;
    public bool PropertiesWritten;
    public BundleContainer? Collection;
    public BundleContainer? Model;
    public BundleContainer? System;
    public BundleLevel? Level;
    public BundleMaterial? Material;
    public BundleColor? Color;
    public BundleObject? Parent;
    public BundleObject? Assembly;
    public BundleObject? Host;
    public BundleObject? Room;
  }

  private sealed class DefinitionState
  {
    public int GeometryOrd;
    public int MemberOrd;
  }

  private ObjectState State(BundleObject obj) =>
    _objectState.TryGetValue(obj.K, out var s) ? s : _objectState[obj.K] = new ObjectState();

  private DefinitionState State(BundleDefinition definition) =>
    _definitionState.TryGetValue(definition.K, out var s) ? s : _definitionState[definition.K] = new DefinitionState();

  // ── properties ────────────────────────────────────────────────────────────────────────────────────────

  /// <summary>
  /// Writes an object's properties and root scalars — once. <paramref name="properties"/> is the nested tree
  /// (<c>properties.*</c> in the bundle); the root scalars are what every producer stamps beside it.
  /// </summary>
  /// <param name="name">Root <c>name</c>.</param>
  /// <param name="speckleType">Root <c>speckle_type</c> — what the v3 graph would have carried.</param>
  /// <param name="sourceType">Root <c>type</c> — the host's own type (Rhino ObjectType, Revit category …).</param>
  /// <param name="units">Root <c>units</c>; <see cref="Units"/> when null.</param>
  /// <param name="typeKey">Stable per-type identity (Revit type element UniqueId). When set, <c>Type Parameters</c> /
  /// <c>System Type Parameters</c> under <c>properties.Parameters</c> are deduplicated into the type tables.</param>
  /// <param name="rootScalars">Any further root scalars (Revit: <c>category</c>, <c>family</c>).</param>
  /// <exception cref="InvalidOperationException">Properties were already written for this object.</exception>
  public BundleObject SetProperties(
    BundleObject obj,
    IReadOnlyDictionary<string, object?>? properties,
    string? name = null,
    string? speckleType = null,
    string? sourceType = null,
    string? units = null,
    string? typeKey = null,
    IEnumerable<KeyValuePair<string, object?>>? rootScalars = null
  )
  {
    var state = State(obj);
    if (state.PropertiesWritten)
    {
      throw new InvalidOperationException(
        $"Properties for '{obj.ApplicationId}' were already written; an object's properties are written once."
      );
    }
    WriteProperties(obj, properties, name, speckleType, sourceType, units, typeKey, rootScalars);
    state.PropertiesWritten = true;
    obj.Name = name;
    return obj;
  }

  // ── object geometry ───────────────────────────────────────────────────────────────────────────────────

  /// <summary>The assembly <paramref name="obj"/> was declared a member of, if any.</summary>
  public BundleObject? AssemblyOf(BundleObject obj) => State(obj).Assembly;

  /// <summary>Whether <see cref="SetProperties"/> has run for <paramref name="obj"/>.</summary>
  public bool PropertiesWritten(BundleObject obj) => State(obj).PropertiesWritten;

  /// <summary>Geometry written for <paramref name="obj"/> so far, in call order. Excludes centerlines.</summary>
  public IReadOnlyList<BundleGeometry> GeometriesOf(BundleObject obj) => State(obj).Geometries;

  /// <summary>Render geometry (<c>DISPLAY</c>), SGEO-encoded now. Ordinal = call order, counted per relation:
  /// an object's first display mesh is ord 0 whether or not a solid preceded it.</summary>
  public BundleGeometry AddGeometry(BundleObject obj, Base geometry, string? geometryKey = null)
  {
    var state = State(obj);
    int ord = state.DisplayOrd++;
    string key = geometryKey ?? $"{obj.ApplicationId}:g{ord}";
    int gK = Pipeline.AddGeometry(key, geometry);
    Pipeline.Display(obj.K, gK, ord);
    var g = RegisterGeometry(key, new BundleGeometry(gK, ord));
    state.Geometries.Add(g);
    return g;
  }

  /// <summary>Authoritative host solid (<c>SOLID</c>): raw bytes kept verbatim (a 3dm brep, <c>type = "3dm"</c>) so a
  /// host that can import them bakes the real solid instead of the display mesh.</summary>
  public BundleGeometry AddRawGeometry(BundleObject obj, byte[] content, string type, string? geometryKey = null)
  {
    var state = State(obj);
    int ord = state.SolidOrd++;
    string key = geometryKey ?? $"{obj.ApplicationId}:raw{ord}";
    int gK = Pipeline.AddRawGeometry(key, content, type);
    Pipeline.Solid(obj.K, gK, ord);
    var g = RegisterGeometry(key, new BundleGeometry(gK, ord));
    state.Geometries.Add(g);
    return g;
  }

  /// <summary>
  /// An object's centerline (<c>CENTERLINE</c>), SGEO-encoded now — a duct/pipe/conduit axis, a framing member's
  /// axis. Its own ordinal counter, like display and solid.
  /// </summary>
  /// <remarks>
  /// NOT a render edge, and deliberately absent from <see cref="GeometriesOf"/>: a consumer that drew every
  /// geometry an object owns would draw the axis through the middle of it, so a producer walking geometry to
  /// paint or bake must not reach the centerline. Read it back through <c>ModelObject.Centerlines</c>.
  /// </remarks>
  public BundleGeometry AddCenterline(BundleObject obj, Base curve, string? geometryKey = null)
  {
    var state = State(obj);
    int ord = state.CenterlineOrd++;
    string key = geometryKey ?? $"{obj.ApplicationId}:cl{ord}";
    int gK = Pipeline.AddGeometry(key, curve);
    Pipeline.Centerline(obj.K, gK, ord);
    return RegisterGeometry(key, new BundleGeometry(gK, ord));
  }

  /// <summary>Renders <paramref name="obj"/> through a placement of <paramref name="definition"/>
  /// (<c>DISPLAY_INSTANCE</c>). <paramref name="transform"/> is 16 row-major values. An object may place several.</summary>
  public BundleInstance Place(
    BundleObject obj,
    BundleDefinition definition,
    IReadOnlyList<double> transform,
    string? units = null,
    string? key = null
  )
  {
    var state = State(obj);
    int ord = state.PlacementOrd++;
    int instK = Pipeline.AddInstance(key ?? $"{obj.ApplicationId}:inst{ord}", definition.K, transform, units ?? Units);
    Pipeline.DisplayInstance(obj.K, instK, ord);
    return new BundleInstance(instK, definition);
  }

  // ── definition geometry ───────────────────────────────────────────────────────────────────────────────

  /// <summary>Definition geometry (<c>DEFINES</c>). <paramref name="memberOrd"/> groups several geometries under one
  /// member (a member's solid and its display meshes share an ordinal); null = next ordinal.</summary>
  public BundleGeometry AddGeometry(
    BundleDefinition definition,
    Base geometry,
    string? geometryKey = null,
    int? memberOrd = null
  )
  {
    var state = State(definition);
    int ord = memberOrd ?? state.GeometryOrd++;
    int gK = Pipeline.AddGeometry(geometryKey ?? $"{definition.Key}:g{ord}", geometry);
    Pipeline.Defines(definition.K, gK, ord);
    return new BundleGeometry(gK, ord);
  }

  /// <summary>Raw host-format definition geometry (a 3dm brep), kept losslessly next to its display meshes.</summary>
  public BundleGeometry AddRawGeometry(
    BundleDefinition definition,
    byte[] content,
    string type,
    string? geometryKey = null,
    int? memberOrd = null
  )
  {
    var state = State(definition);
    int ord = memberOrd ?? state.GeometryOrd++;
    int gK = Pipeline.AddRawGeometry(geometryKey ?? $"{definition.Key}:raw{ord}", content, type);
    Pipeline.Defines(definition.K, gK, ord);
    return new BundleGeometry(gK, ord);
  }

  /// <summary>A nested placement inside a definition (<c>DEFINES_INSTANCE</c>): a block that contains a block.</summary>
  public BundleInstance PlaceNested(
    BundleDefinition definition,
    BundleDefinition nested,
    IReadOnlyList<double> transform,
    string? units,
    string? key = null
  )
  {
    var state = State(definition);
    int ord = state.GeometryOrd++;
    int instK = Pipeline.AddInstance(key ?? $"{definition.Key}:inst{ord}", nested.K, transform, units);
    Pipeline.DefinesInstance(definition.K, instK, ord);
    return new BundleInstance(instK, nested);
  }

  /// <summary>
  /// An authored member of a definition that owns its own geometry (Rhino / AutoCAD block contents): the member's
  /// object row carries its layer and properties, its geometry renders ONLY through placements of the definition —
  /// so it gets <c>DEFINES</c> + <c>DEFINES_MEMBER</c> on one member ordinal and no <c>DISPLAY</c> edge of its own.
  /// Returns the geometry handles (paint them with <see cref="HasMaterial"/> as usual).
  /// </summary>
  public IReadOnlyList<BundleGeometry> AddMember(
    BundleDefinition definition,
    BundleObject member,
    IEnumerable<Base> geometry,
    int? memberOrd = null
  )
  {
    int ord = memberOrd ?? NextMemberOrdinal(definition);
    Pipeline.DefinesMember(definition.K, member.K, ord);
    var handles = new List<BundleGeometry>();
    int i = 0;
    foreach (var g in geometry)
    {
      string key = $"{member.ApplicationId}:g{i++}";
      int gK = Pipeline.AddGeometry(key, g);
      Pipeline.Defines(definition.K, gK, ord);
      handles.Add(RegisterGeometry(key, new BundleGeometry(gK, ord)));
    }
    Bump(definition, ord);
    return handles;
  }

  /// <summary>A member's raw host geometry (a 3dm solid) alongside its display meshes — same member ordinal, so a
  /// receiver can pick the solid over its shadow. Call after <see cref="AddMember"/> with the ordinal it returned
  /// via <see cref="BundleGeometry.Ord"/>.</summary>
  public BundleGeometry AddMemberRawGeometry(
    BundleDefinition definition,
    BundleObject member,
    byte[] content,
    string type,
    int memberOrd
  )
  {
    string key = $"{member.ApplicationId}:raw{memberOrd}";
    int gK = Pipeline.AddRawGeometry(key, content, type);
    Pipeline.Defines(definition.K, gK, memberOrd);
    return RegisterGeometry(key, new BundleGeometry(gK, memberOrd));
  }

  /// <summary>A member that is itself a placement of another definition (a block inside a block): <c>INSTANCE</c> +
  /// <c>DEFINES_INSTANCE</c> + <c>DEFINES_MEMBER</c> + <c>PLACES</c> (member → its placement), no
  /// <c>DISPLAY_INSTANCE</c>.</summary>
  public BundleInstance AddMemberPlacement(
    BundleDefinition definition,
    BundleObject member,
    BundleDefinition nested,
    IReadOnlyList<double> transform,
    string? units = null,
    int? memberOrd = null
  )
  {
    int ord = memberOrd ?? NextMemberOrdinal(definition);
    int instK = Pipeline.AddInstance(member.ApplicationId, nested.K, transform, units ?? Units);
    Pipeline.DefinesInstance(definition.K, instK, ord);
    Pipeline.DefinesMember(definition.K, member.K, ord);
    Pipeline.Places(member.K, instK);
    Bump(definition, ord);
    return new BundleInstance(instK, nested);
  }

  /// <summary>References geometry already written elsewhere (Revit: a family's mesh added under an element, shared by
  /// the symbol) as a definition's geometry (<c>DEFINES</c>), without re-encoding.</summary>
  public void AddExistingGeometry(BundleDefinition definition, BundleGeometry geometry, int? memberOrd = null)
  {
    int ord = memberOrd ?? NextMemberOrdinal(definition);
    Pipeline.Defines(definition.K, geometry.K, ord);
    Bump(definition, ord);
  }

  /// <summary>The next unused member ordinal. The (definition, ordinal) pair joins a member's object row to its
  /// geometry, which is what survives content-hash geometry dedup.</summary>
  public int NextMemberOrdinal(BundleDefinition definition)
  {
    var state = State(definition);
    return Math.Max(state.GeometryOrd, state.MemberOrd);
  }

  private void Bump(BundleDefinition definition, int ord)
  {
    var state = State(definition);
    state.MemberOrd = Math.Max(state.MemberOrd, ord + 1);
    state.GeometryOrd = Math.Max(state.GeometryOrd, ord + 1);
  }

  // ── geometry appearance ───────────────────────────────────────────────────────────────────────────────

  /// <summary>Geometry-plane material (<c>HAS_MATERIAL</c>): the intrinsic render material, strongest tier.</summary>
  public void HasMaterial(BundleGeometry geometry, BundleMaterial material) =>
    Pipeline.HasMaterial(geometry.K, material.K);

  /// <summary>Geometry-plane colour (<c>HAS_COLOR</c>).</summary>
  public void HasColor(BundleGeometry geometry, BundleColor color) => Pipeline.HasColor(geometry.K, color.K);

  // ── object → node ─────────────────────────────────────────────────────────────────────────────────────

  /// <summary>Authored scene-tree container (<c>IN_COLLECTION</c>).</summary>
  public void InCollection(BundleObject obj, BundleContainer collection)
  {
    var state = State(obj);
    if (ReferenceEquals(state.Collection, collection))
    {
      return;
    }
    ThrowIfAlreadySet(state.Collection);
    state.Collection = collection;
    Pipeline.InCollection(obj.K, collection.K, 0);
  }

  /// <summary>Federated-model container (<c>IN_MODEL</c>): the Revit host / linked model the object came from.</summary>
  public void InModel(BundleObject obj, BundleContainer model)
  {
    var state = State(obj);
    if (ReferenceEquals(state.Model, model))
    {
      return;
    }
    ThrowIfAlreadySet(state.Model);
    state.Model = model;
    Pipeline.InModel(obj.K, model.K, 0);
  }

  /// <summary>MEP system / network container (<c>IN_SYSTEM</c>).</summary>
  public void InSystem(BundleObject obj, BundleContainer system)
  {
    var state = State(obj);
    if (ReferenceEquals(state.System, system))
    {
      return;
    }
    ThrowIfAlreadySet(state.System);
    state.System = system;
    Pipeline.InSystem(obj.K, system.K, 0);
  }

  /// <summary>Storey (<c>ON_LEVEL</c>).</summary>
  public void OnLevel(BundleObject obj, BundleLevel level)
  {
    var state = State(obj);
    if (ReferenceEquals(state.Level, level))
    {
      return;
    }
    ThrowIfAlreadySet(state.Level);
    state.Level = level;
    Pipeline.OnLevel(obj.K, level.K);
  }

  /// <summary>Object-plane material (<c>OBJECT_HAS_MATERIAL</c>): fills where a geometry has none of its own.</summary>
  public void ObjectHasMaterial(BundleObject obj, BundleMaterial material)
  {
    var state = State(obj);
    if (ReferenceEquals(state.Material, material))
    {
      return;
    }
    ThrowIfAlreadySet(state.Material);
    state.Material = material;
    Pipeline.ObjectHasMaterial(obj.K, material.K);
  }

  /// <summary>Object-plane colour (<c>OBJECT_HAS_COLOR</c>): overrides the geometry's own.</summary>
  public void ObjectHasColor(BundleObject obj, BundleColor color)
  {
    var state = State(obj);
    if (ReferenceEquals(state.Color, color))
    {
      return;
    }
    ThrowIfAlreadySet(state.Color);
    state.Color = color;
    Pipeline.ObjectHasColor(obj.K, color.K);
  }

  /// <summary>Authored group membership (<c>IN_GROUP</c>) — an object may sit in several, nested or not.</summary>
  public void InGroup(BundleObject obj, BundleContainer group, int ord = 0) => Pipeline.InGroup(obj.K, group.K, ord);

  /// <summary>Paints a container's authored render material (<c>NODE_HAS_MATERIAL</c>): a layer or tag material.
  /// Weakest tier of the material ladder — geometry, then object, then this.</summary>
  public void NodeHasMaterial(BundleContainer node, BundleMaterial material)
  {
    if (_nodeMaterial.TryGetValue(node.K, out int already) && already == material.K)
    {
      return;
    }
    _nodeMaterial[node.K] = material.K;
    Pipeline.NodeHasMaterial(node.K, material.K);
  }

  /// <summary>Paints a container's display colour (<c>NODE_HAS_COLOR</c>): the ByLayer default that applies when
  /// nothing more specific does.</summary>
  public void NodeHasColor(BundleContainer node, BundleColor color)
  {
    if (_nodeColor.TryGetValue(node.K, out int already) && already == color.K)
    {
      return;
    }
    _nodeColor[node.K] = color.K;
    Pipeline.NodeHasColor(node.K, color.K);
  }

  // ── object → object ───────────────────────────────────────────────────────────────────────────────────

  /// <summary>Declares <paramref name="child"/> a component of <paramref name="parent"/> (<c>SUBELEMENT</c>).
  /// <paramref name="ord"/> is the child's position; null = next.</summary>
  public void Subelement(BundleObject parent, BundleObject child, int? ord = null)
  {
    var childState = State(child);
    if (childState.Parent is not null)
    {
      if (ReferenceEquals(childState.Parent, parent))
      {
        return;
      }
      throw new InvalidOperationException(
        $"Object '{child.ApplicationId}' already has parent '{childState.Parent.ApplicationId}'; a bundle edge cannot be retracted."
      );
    }
    var parentState = State(parent);
    int o = ord ?? parentState.ChildOrd;
    parentState.ChildOrd = Math.Max(parentState.ChildOrd, o + 1);
    childState.Parent = parent;
    Pipeline.Subelement(parent.K, child.K, o);
  }

  /// <summary>Declares <paramref name="member"/> a member of <paramref name="assembly"/> (<c>IN_ASSEMBLY</c>):
  /// authored fabrication membership, separate from <see cref="Subelement"/> ownership. <paramref name="ord"/> is the
  /// member's position — 0 is the main member, ≥1 orders secondary or nested-assembly members; null = next. A member
  /// belongs to one assembly.</summary>
  public void InAssembly(BundleObject assembly, BundleObject member, int? ord = null)
  {
    var memberState = State(member);
    if (memberState.Assembly is not null)
    {
      if (ReferenceEquals(memberState.Assembly, assembly))
      {
        return;
      }
      throw new InvalidOperationException(
        $"Object '{member.ApplicationId}' is already a member of assembly '{memberState.Assembly.ApplicationId}'; a bundle edge cannot be retracted."
      );
    }
    var assemblyState = State(assembly);
    int o = ord ?? assemblyState.AssemblyMemberOrd;
    assemblyState.AssemblyMemberOrd = Math.Max(assemblyState.AssemblyMemberOrd, o + 1);
    memberState.Assembly = assembly;
    Pipeline.InAssembly(member.K, assembly.K, o);
  }

  /// <summary>Host (<c>HOSTED_ON</c>): the wall a door is placed on. Not ownership.</summary>
  public void HostedOn(BundleObject obj, BundleObject host)
  {
    var state = State(obj);
    if (ReferenceEquals(state.Host, host))
    {
      return;
    }
    ThrowIfAlreadySet(state.Host);
    state.Host = host;
    Pipeline.HostedOn(obj.K, host.K);
  }

  /// <summary>Containing room object (<c>IN_ROOM</c>).</summary>
  public void InRoom(BundleObject obj, BundleObject room)
  {
    var state = State(obj);
    if (ReferenceEquals(state.Room, room))
    {
      return;
    }
    ThrowIfAlreadySet(state.Room);
    state.Room = room;
    Pipeline.InRoom(obj.K, room.K, 0);
  }

  /// <summary>MEP connectivity (<c>CONNECTS_TO</c>); <paramref name="scope"/> is the producer's connection scope tag.</summary>
  public void ConnectsTo(BundleObject obj, BundleObject other, int scope = 0) =>
    Pipeline.ConnectsTo(obj.K, other.K, scope);

  /// <summary>This object bounds <paramref name="room"/> (<c>BOUNDS</c>): a wall enclosing a room.</summary>
  public void Bounds(BundleObject obj, BundleObject room, int ord = 0) => Pipeline.Bounds(obj.K, room.K, ord);

  private static void ThrowIfAlreadySet(object? existing)
  {
    if (existing is not null)
    {
      // The edge is on disk already: pointing elsewhere cannot take it back, and letting it through would write a
      // second edge for a single-valued relation.
      throw new InvalidOperationException("This relation was already set; a bundle edge cannot be retracted.");
    }
  }
}
