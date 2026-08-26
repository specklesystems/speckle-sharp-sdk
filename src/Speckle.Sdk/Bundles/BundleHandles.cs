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

/// <summary>A CONTAINER node: layer, category, folder, federated model, group, MEP system … (<see cref="Subtype"/>).</summary>
public sealed class BundleCollection : BundleNode
{
  internal BundleCollection(
    BundleBuilder builder,
    int k,
    string key,
    string? name,
    string subtype,
    BundleCollection? parent
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
  public BundleCollection? Parent { get; }

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

  /// <summary>Declares <paramref name="member"/> an authored member of this definition (<c>DEFINES_MEMBER</c>, ordinal
  /// shared with the member's geometry); a nested-block member also gets <c>PLACES</c> to its placement.</summary>
  public void AddMember(BundleObject member, int? memberOrd = null, BundleInstance? placement = null)
  {
    Builder.Pipeline.DefinesMember(K, member.K, memberOrd ?? _memberOrd++);
    if (placement is not null)
    {
      Builder.Pipeline.Places(member.K, placement.K);
    }
  }

  /// <summary>Next member ordinal — hand it to both <see cref="AddGeometry"/> and <see cref="AddMember"/> so a member's
  /// geometry and its object row join on the same ordinal.</summary>
  public int NextMemberOrdinal() => Math.Max(_geometryOrd, _memberOrd);
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
  private int _ord;
  private int _placementOrd;
  private BundleCollection? _collection;
  private BundleCollection? _model;
  private BundleCollection? _system;
  private BundleLevel? _level;
  private BundleMaterial? _material;
  private BundleColor? _color;
  private BundleObject? _parent;
  private BundleObject? _host;
  private BundleObject? _room;

  internal BundleObject(BundleBuilder builder, int k, string applicationId, string? name)
  {
    _builder = builder;
    K = k;
    ApplicationId = applicationId;
    Name = name;
  }

  /// <summary>Dense object index inside this bundle.</summary>
  public int K { get; }

  public string ApplicationId { get; }
  public string? Name { get; }

  public IReadOnlyList<BundleGeometry> Geometries => _geometries;

  // ── geometry ──────────────────────────────────────────────────────────────────────────────────────────

  /// <summary>Render geometry (<c>DISPLAY</c>), SGEO-encoded now. Ordinal = call order.</summary>
  public BundleGeometry AddGeometry(Base geometry, string? geometryKey = null)
  {
    int ord = _ord++;
    int gK = _builder.Pipeline.AddGeometry(geometryKey ?? $"{ApplicationId}:g{ord}", geometry);
    _builder.Pipeline.Display(K, gK, ord);
    var g = new BundleGeometry(_builder, gK, ord);
    _geometries.Add(g);
    return g;
  }

  /// <summary>Authoritative host solid (<c>SOLID</c>): raw bytes kept verbatim (a 3dm brep, <c>type = "3dm"</c>) so a
  /// host that can import them bakes the real solid instead of the display mesh.</summary>
  public BundleGeometry AddRawGeometry(byte[] content, string type, string? geometryKey = null)
  {
    int ord = _ord++;
    int gK = _builder.Pipeline.AddRawGeometry(geometryKey ?? $"{ApplicationId}:raw{ord}", content, type);
    _builder.Pipeline.Solid(K, gK, ord);
    var g = new BundleGeometry(_builder, gK, ord);
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
  public BundleCollection? Collection
  {
    get => _collection;
    set => Set(ref _collection, value, k => _builder.Pipeline.InCollection(K, k, 0));
  }

  /// <summary>Federated-model container (<c>IN_MODEL</c>): the Revit host / linked model the object came from.</summary>
  public BundleCollection? Model
  {
    get => _model;
    set => Set(ref _model, value, k => _builder.Pipeline.InModel(K, k, 0));
  }

  /// <summary>MEP system / network container (<c>IN_SYSTEM</c>).</summary>
  public BundleCollection? System
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
  public void AddToGroup(BundleCollection group, int ord = 0) => _builder.Pipeline.InGroup(K, group.K, ord);

  // ── object → object ───────────────────────────────────────────────────────────────────────────────────

  /// <summary>Owning element (<c>SUBELEMENT</c>): this object is a component of <c>Parent</c>.</summary>
  public BundleObject? Parent
  {
    get => _parent;
    set => Set(ref _parent, value, k => _builder.Pipeline.Subelement(k, K, value!._childOrd++));
  }

  private int _childOrd;

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
    if (value is null || ReferenceEquals(field, value))
    {
      field = value;
      return;
    }
    if (field is not null)
    {
      throw new InvalidOperationException("This relation was already set; a bundle edge cannot be retracted.");
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
