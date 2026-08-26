using Speckle.Sdk.Models;

namespace Speckle.Sdk.Bundles;

/// <summary>A node handle: carries the dense K the pipeline assigned. Handles are only valid on the builder that made them.</summary>
public abstract class BundleNode
{
  private protected BundleNode(BundleBuilder builder, int k)
  {
    Builder = builder;
    K = k;
  }

  internal BundleBuilder Builder { get; }

  /// <summary>Dense node index inside this bundle.</summary>
  public int K { get; }

  private BundleMaterial? _material;
  private BundleColor? _color;

  /// <summary>Material painted on the node itself (<c>NODE_HAS_MATERIAL</c>): a layer's render material, a placement's override.</summary>
  public BundleMaterial? Material
  {
    get => _material;
    set
    {
      if (value is not null && !ReferenceEquals(_material, value))
      {
        Builder.Pipeline.NodeHasMaterial(K, value.K);
      }
      _material = value;
    }
  }

  /// <summary>Colour painted on the node itself (<c>NODE_HAS_COLOR</c>): a layer / tag display colour.</summary>
  public BundleColor? Color
  {
    get => _color;
    set
    {
      if (value is not null && !ReferenceEquals(_color, value))
      {
        Builder.Pipeline.NodeHasColor(K, value.K);
      }
      _color = value;
    }
  }
}

/// <summary>A CONTAINER node (spec node kind 7): layer, category, folder, federated model, group, MEP system …
/// (<see cref="Subtype"/>). Objects reference it through a rel — <c>IN_COLLECTION</c> for the authored scene tree,
/// <c>IN_MODEL</c>, <c>IN_GROUP</c>, <c>IN_SYSTEM</c> for the other axes.</summary>
public sealed class BundleContainer : BundleNode
{
  internal BundleContainer(
    BundleBuilder builder,
    int k,
    string key,
    string? name,
    string subtype,
    BundleContainer? parent
  )
    : base(builder, k)
  {
    Key = key;
    Name = name;
    Subtype = subtype;
    Parent = parent;
  }

  public string Key { get; }
  public string? Name { get; }
  public string Subtype { get; }
  public BundleContainer? Parent { get; }

  public override string ToString() => $"{Subtype} '{Name}'";
}

public sealed class BundleLevel : BundleNode
{
  internal BundleLevel(BundleBuilder builder, int k, string key, string? name, double elevation)
    : base(builder, k)
  {
    Key = key;
    Name = name;
    Elevation = elevation;
  }

  public string Key { get; }
  public string? Name { get; }
  public double Elevation { get; }
}

public sealed class BundleMaterial : BundleNode
{
  internal BundleMaterial(BundleBuilder builder, int k, string key, string? name, int argb)
    : base(builder, k)
  {
    Key = key;
    Name = name;
    Argb = argb;
  }

  public string Key { get; }
  public string? Name { get; }
  public int Argb { get; }
}

public sealed class BundleColor : BundleNode
{
  internal BundleColor(BundleBuilder builder, int k, int argb)
    : base(builder, k)
  {
    Argb = argb;
  }

  public int Argb { get; }
}

/// <summary>A DEFINITION node (block / family symbol): geometry shared by every placement, plus nested placements
/// and member objects.</summary>
public sealed class BundleDefinition : BundleNode
{
  private int _geometryOrd;
  private int _memberOrd;

  internal BundleDefinition(BundleBuilder builder, int k, string key, string? name)
    : base(builder, k)
  {
    Key = key;
    Name = name;
  }

  public string Key { get; }
  public string? Name { get; }

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
    BundleDefinition definition,
    IReadOnlyList<double> transform,
    string? units,
    string? key = null
  )
  {
    int ord = _geometryOrd++;
    int instK = Builder.Pipeline.AddInstance(key ?? $"{Key}:inst{ord}", definition.K, transform, units);
    Builder.Pipeline.DefinesInstance(K, instK, ord);
    return new BundleInstance(Builder, instK, definition);
  }

  /// <summary>
  /// An authored member of this definition that owns its own geometry (Rhino / AutoCAD block contents): the member's
  /// object row carries its layer and properties, its geometry renders ONLY through placements of this definition —
  /// so it gets <c>DEFINES</c> + <c>DEFINES_MEMBER</c> on one member ordinal and no <c>DISPLAY</c> edge of its own.
  /// Returns the geometry handles (set <see cref="BundleGeometry.Material"/> on them as usual).
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
  /// receiver can pick the solid over its shadow. Call after <see cref="AddMember(BundleObject, IEnumerable{Base}, int?)"/>
  /// with the ordinal it returned via <see cref="BundleGeometry.Ord"/>.</summary>
  public BundleGeometry AddMemberRawGeometry(BundleObject member, byte[] content, string type, int memberOrd)
  {
    string key = $"{member.ApplicationId}:raw{memberOrd}";
    int gK = Builder.Pipeline.AddRawGeometry(key, content, type);
    Builder.Pipeline.Defines(K, gK, memberOrd);
    return Builder.RegisterGeometry(key, new BundleGeometry(Builder, gK, memberOrd));
  }

  /// <summary>A member that is itself a placement of another definition (a block inside a block): <c>INSTANCE</c> +
  /// <c>DEFINES_INSTANCE</c> + <c>DEFINES_MEMBER</c> + <c>PLACES</c> (member → its placement), no <c>DISPLAY_INSTANCE</c>.</summary>
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

/// <summary>An INSTANCE node: one placement of a <see cref="BundleDefinition"/>.</summary>
public sealed class BundleInstance : BundleNode
{
  internal BundleInstance(BundleBuilder builder, int k, BundleDefinition definition)
    : base(builder, k)
  {
    Definition = definition;
  }

  public BundleDefinition Definition { get; }
}

/// <summary>One geometry (SGEO mesh or raw host blob) already written; set <see cref="Material"/> / <see cref="Color"/>
/// for per-geometry appearance (<c>HAS_MATERIAL</c> / <c>HAS_COLOR</c>, the geometry plane).</summary>
public sealed class BundleGeometry
{
  private readonly BundleBuilder _builder;
  private BundleMaterial? _material;
  private BundleColor? _color;

  internal BundleGeometry(BundleBuilder builder, int k, int ord)
  {
    _builder = builder;
    K = k;
    Ord = ord;
  }

  /// <summary>Dense geometry index inside this bundle.</summary>
  public int K { get; }

  /// <summary>Draw order within its owner.</summary>
  public int Ord { get; }

  public BundleMaterial? Material
  {
    get => _material;
    set
    {
      if (value is not null && !ReferenceEquals(_material, value))
      {
        _builder.Pipeline.HasMaterial(K, value.K);
      }
      _material = value;
    }
  }

  public BundleColor? Color
  {
    get => _color;
    set
    {
      if (value is not null && !ReferenceEquals(_color, value))
      {
        _builder.Pipeline.HasColor(K, value.K);
      }
      _color = value;
    }
  }
}

/// <summary>
/// An object being written: the property carrier a host element becomes. Geometry, placements and every relation
/// hang off it; each setter writes its edge immediately, so set each relation once.
/// </summary>
public sealed class BundleObject
{
  private readonly BundleBuilder _builder;
  private readonly List<BundleGeometry> _geometries = new();
  private int _displayOrd;
  private int _solidOrd;
  private int _placementOrd;
  private BundleContainer? _collection;
  private BundleContainer? _model;
  private BundleContainer? _system;
  private BundleLevel? _level;
  private BundleMaterial? _material;
  private BundleColor? _color;
  private BundleObject? _parent;
  private BundleObject? _host;
  private BundleObject? _room;

  internal BundleObject(BundleBuilder builder, int k, string applicationId)
  {
    _builder = builder;
    K = k;
    ApplicationId = applicationId;
  }

  /// <summary>Dense object index inside this bundle.</summary>
  public int K { get; }

  public string ApplicationId { get; }
  public string? Name { get; private set; }

  /// <summary>Whether <see cref="SetProperties"/> has been called.</summary>
  public bool PropertiesWritten { get; private set; }

  /// <summary>
  /// Writes the object's properties and root scalars — once. <paramref name="properties"/> is the nested tree
  /// (<c>properties.*</c> in the bundle); the root scalars are what every producer stamps beside it.
  /// </summary>
  /// <param name="name">Root <c>name</c>.</param>
  /// <param name="speckleType">Root <c>speckle_type</c> — what the v3 graph would have carried.</param>
  /// <param name="sourceType">Root <c>type</c> — the host's own type (Rhino ObjectType, Revit category …).</param>
  /// <param name="units">Root <c>units</c>; the builder's <see cref="BundleBuilder.Units"/> when null.</param>
  /// <param name="typeKey">Stable per-type identity (Revit type element UniqueId). When set, <c>Type Parameters</c> /
  /// <c>System Type Parameters</c> under <c>properties.Parameters</c> are deduplicated into the type tables.</param>
  /// <param name="rootScalars">Any further root scalars (Revit: <c>category</c>, <c>family</c>).</param>
  /// <exception cref="InvalidOperationException">Properties were already written for this object.</exception>
  public BundleObject SetProperties(
    IReadOnlyDictionary<string, object?>? properties,
    string? name = null,
    string? speckleType = null,
    string? sourceType = null,
    string? units = null,
    string? typeKey = null,
    IEnumerable<KeyValuePair<string, object?>>? rootScalars = null
  )
  {
    if (PropertiesWritten)
    {
      throw new InvalidOperationException(
        $"Properties for '{ApplicationId}' were already written; an object's properties are written once."
      );
    }
    _builder.WriteProperties(this, properties, name, speckleType, sourceType, units, typeKey, rootScalars);
    PropertiesWritten = true;
    Name = name;
    return this;
  }

  public IReadOnlyList<BundleGeometry> Geometries => _geometries;

  // ── geometry ──────────────────────────────────────────────────────────────────────────────────────────

  /// <summary>Render geometry (<c>DISPLAY</c>), SGEO-encoded now. Ordinal = call order, counted per relation:
  /// an object's first display mesh is ord 0 whether or not a solid preceded it.</summary>
  public BundleGeometry AddGeometry(Base geometry, string? geometryKey = null)
  {
    int ord = _displayOrd++;
    string key = geometryKey ?? $"{ApplicationId}:g{ord}";
    int gK = _builder.Pipeline.AddGeometry(key, geometry);
    _builder.Pipeline.Display(K, gK, ord);
    var g = _builder.RegisterGeometry(key, new BundleGeometry(_builder, gK, ord));
    _geometries.Add(g);
    return g;
  }

  /// <summary>Authoritative host solid (<c>SOLID</c>): raw bytes kept verbatim (a 3dm brep, <c>type = "3dm"</c>) so a
  /// host that can import them bakes the real solid instead of the display mesh.</summary>
  public BundleGeometry AddRawGeometry(byte[] content, string type, string? geometryKey = null)
  {
    int ord = _solidOrd++;
    string key = geometryKey ?? $"{ApplicationId}:raw{ord}";
    int gK = _builder.Pipeline.AddRawGeometry(key, content, type);
    _builder.Pipeline.Solid(K, gK, ord);
    var g = _builder.RegisterGeometry(key, new BundleGeometry(_builder, gK, ord));
    _geometries.Add(g);
    return g;
  }

  /// <summary>Renders this object through a placement of <paramref name="definition"/> (<c>DISPLAY_INSTANCE</c>).
  /// <paramref name="transform"/> is 16 row-major values. An object may place several instances.</summary>
  public BundleInstance Place(
    BundleDefinition definition,
    IReadOnlyList<double> transform,
    string? units = null,
    string? key = null
  )
  {
    int ord = _placementOrd++;
    int instK = _builder.Pipeline.AddInstance(
      key ?? $"{ApplicationId}:inst{ord}",
      definition.K,
      transform,
      units ?? _builder.Units
    );
    _builder.Pipeline.DisplayInstance(K, instK, ord);
    return new BundleInstance(_builder, instK, definition);
  }

  // ── object → node ─────────────────────────────────────────────────────────────────────────────────────

  /// <summary>Authored scene-tree container (<c>IN_COLLECTION</c>).</summary>
  public BundleContainer? Collection
  {
    get => _collection;
    set => Set(ref _collection, value, k => _builder.Pipeline.InCollection(K, k, 0));
  }

  /// <summary>Federated-model container (<c>IN_MODEL</c>): the Revit host / linked model the object came from.</summary>
  public BundleContainer? Model
  {
    get => _model;
    set => Set(ref _model, value, k => _builder.Pipeline.InModel(K, k, 0));
  }

  /// <summary>MEP system / network container (<c>IN_SYSTEM</c>).</summary>
  public BundleContainer? System
  {
    get => _system;
    set => Set(ref _system, value, k => _builder.Pipeline.InSystem(K, k, 0));
  }

  /// <summary>Storey (<c>ON_LEVEL</c>).</summary>
  public BundleLevel? Level
  {
    get => _level;
    set => Set(ref _level, value, k => _builder.Pipeline.OnLevel(K, k));
  }

  /// <summary>Object-plane material (<c>OBJECT_HAS_MATERIAL</c>): fills where a geometry has none of its own.</summary>
  public BundleMaterial? Material
  {
    get => _material;
    set => Set(ref _material, value, k => _builder.Pipeline.ObjectHasMaterial(K, k));
  }

  /// <summary>Object-plane colour (<c>OBJECT_HAS_COLOR</c>): overrides the geometry's own.</summary>
  public BundleColor? Color
  {
    get => _color;
    set => Set(ref _color, value, k => _builder.Pipeline.ObjectHasColor(K, k));
  }

  /// <summary>Authored group membership (<c>IN_GROUP</c>) — an object may sit in several, nested or not.</summary>
  public void AddToGroup(BundleContainer group, int ord = 0) => _builder.Pipeline.InGroup(K, group.K, ord);

  // ── object → object ───────────────────────────────────────────────────────────────────────────────────

  /// <summary>Owning element (<c>SUBELEMENT</c>): this object is a component of <c>Parent</c>. Sugar for
  /// <c>value.AddChild(this)</c> with the next child ordinal.</summary>
  public BundleObject? Parent
  {
    get => _parent;
    set
    {
      if (value is not null && !ReferenceEquals(_parent, value))
      {
        value.AddChild(this);
      }
    }
  }

  private int _childOrd;

  /// <summary>Declares <paramref name="child"/> a component of this object (<c>SUBELEMENT</c>). <paramref name="ord"/>
  /// is the child's position; null = next.</summary>
  public void AddChild(BundleObject child, int? ord = null)
  {
    if (child._parent is not null)
    {
      if (ReferenceEquals(child._parent, this))
      {
        return;
      }
      throw new InvalidOperationException(
        $"Object '{child.ApplicationId}' already has parent '{child._parent.ApplicationId}'; a bundle edge cannot be retracted."
      );
    }
    int o = ord ?? _childOrd;
    _childOrd = Math.Max(_childOrd, o + 1);
    child._parent = this;
    _builder.Pipeline.Subelement(K, child.K, o);
  }

  /// <summary>Host (<c>HOSTED_ON</c>): the wall a door is placed on. Not ownership.</summary>
  public BundleObject? Host
  {
    get => _host;
    set => Set(ref _host, value, k => _builder.Pipeline.HostedOn(K, k));
  }

  /// <summary>Containing room object (<c>IN_ROOM</c>).</summary>
  public BundleObject? Room
  {
    get => _room;
    set => Set(ref _room, value, k => _builder.Pipeline.InRoom(K, k, 0));
  }

  /// <summary>MEP connectivity (<c>CONNECTS_TO</c>); <paramref name="scope"/> is the producer's connection scope tag.</summary>
  public void ConnectTo(BundleObject other, int scope = 0) => _builder.Pipeline.ConnectsTo(K, other.K, scope);

  /// <summary>This object bounds <paramref name="room"/> (<c>BOUNDS</c>): a wall enclosing a room.</summary>
  public void Bounds(BundleObject room, int ord = 0) => _builder.Pipeline.Bounds(K, room.K, ord);

  public override string ToString() => Name is null ? ApplicationId : $"{Name} ({ApplicationId})";

  private static void Set<T>(ref T? field, T? value, Action<int> emit)
    where T : class
  {
    if (ReferenceEquals(field, value))
    {
      return;
    }
    if (field is not null)
    {
      // The edge is on disk already: neither clearing (null) nor pointing elsewhere can take it back, and letting
      // null through would let a second assignment write a second edge for a single-valued relation.
      throw new InvalidOperationException("This relation was already set; a bundle edge cannot be retracted.");
    }
    if (value is null)
    {
      return;
    }
    field = value;
    emit(KOf(value));
  }

  private static int KOf(object node) =>
    node switch
    {
      BundleNode n => n.K,
      BundleObject o => o.K,
      _ => throw new ArgumentException("Not a bundle handle", nameof(node)),
    };
}
