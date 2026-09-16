using Speckle.Sdk.Models;

namespace Speckle.Sdk.Bundles.Handles;

/// <summary>The property carrier a host element becomes: its properties, its geometry, and every edge that starts
/// at it. Single-valued relations are write-once — re-pointing one throws, because the edge is already on disk.</summary>
public sealed class BundleObject
{
  private readonly BundleBuilder _builder;
  private List<BundleGeometry>? _geometries;
  private int _displayOrd;
  private int _solidOrd;
  private int _centerlineOrd;
  private int _placementOrd;
  private int _childOrd;
  private int _assemblyMemberOrd;
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

  /// <summary>Whether <see cref="SetProperties"/> has run.</summary>
  public bool PropertiesWritten { get; private set; }

  /// <summary>The assembly this object was declared a member of, if any.</summary>
  public BundleObject? Assembly { get; private set; }

  /// <summary>Geometry written for this object so far, in call order. Excludes centerlines.</summary>
  public IReadOnlyList<BundleGeometry> Geometries =>
    _geometries ?? (IReadOnlyList<BundleGeometry>)Array.Empty<BundleGeometry>();

  // ── properties ────────────────────────────────────────────────────────────────────────────────────────

  /// <summary>
  /// Writes this object's properties and root scalars — once. <paramref name="properties"/> is the nested tree
  /// (<c>properties.*</c> in the bundle); the root scalars are what every producer stamps beside it.
  /// </summary>
  /// <param name="name">Root <c>name</c>.</param>
  /// <param name="speckleType">Root <c>speckle_type</c> — what the v3 graph would have carried.</param>
  /// <param name="sourceType">Root <c>type</c> — the host's own type (Rhino ObjectType, Revit category …).</param>
  /// <param name="units">Root <c>units</c>; the bundle's units when null.</param>
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
    (_geometries ??= new()).Add(g);
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
    (_geometries ??= new()).Add(g);
    return g;
  }

  /// <summary>
  /// This object's centerline (<c>CENTERLINE</c>), SGEO-encoded now — a duct/pipe/conduit axis, a framing member's
  /// axis. Its own ordinal counter, like display and solid.
  /// </summary>
  /// <remarks>
  /// NOT a render edge, and deliberately absent from <see cref="Geometries"/>: a consumer that drew every
  /// geometry an object owns would draw the axis through the middle of it, so a producer walking geometry to
  /// paint or bake must not reach the centerline. Read it back through <c>ModelObject.Centerlines</c>.
  /// </remarks>
  public BundleGeometry AddCenterline(Base curve, string? geometryKey = null)
  {
    int ord = _centerlineOrd++;
    string key = geometryKey ?? $"{ApplicationId}:cl{ord}";
    int gK = _builder.Pipeline.AddGeometry(key, curve);
    _builder.Pipeline.Centerline(K, gK, ord);
    return _builder.RegisterGeometry(key, new BundleGeometry(_builder, gK, ord));
  }

  /// <summary>Renders this object through a placement of <paramref name="definition"/> (<c>DISPLAY_INSTANCE</c>).
  /// <paramref name="transform"/> is 16 row-major values. An object may place several.</summary>
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
    set => Set(ref _collection, value, v => _builder.Pipeline.InCollection(K, v.K, 0));
  }

  /// <summary>Federated-model container (<c>IN_MODEL</c>): the Revit host / linked model the object came from.</summary>
  public BundleContainer? Model
  {
    get => _model;
    set => Set(ref _model, value, v => _builder.Pipeline.InModel(K, v.K, 0));
  }

  /// <summary>MEP system / network container (<c>IN_SYSTEM</c>).</summary>
  public BundleContainer? System
  {
    get => _system;
    set => Set(ref _system, value, v => _builder.Pipeline.InSystem(K, v.K, 0));
  }

  /// <summary>Storey (<c>ON_LEVEL</c>).</summary>
  public BundleLevel? Level
  {
    get => _level;
    set => Set(ref _level, value, v => _builder.Pipeline.OnLevel(K, v.K));
  }

  /// <summary>Object-plane material (<c>OBJECT_HAS_MATERIAL</c>): fills where a geometry has none of its own.</summary>
  public BundleMaterial? Material
  {
    get => _material;
    set => Set(ref _material, value, v => _builder.Pipeline.ObjectHasMaterial(K, v.K));
  }

  /// <summary>Object-plane colour (<c>OBJECT_HAS_COLOR</c>): overrides the geometry's own.</summary>
  public BundleColor? Color
  {
    get => _color;
    set => Set(ref _color, value, v => _builder.Pipeline.ObjectHasColor(K, v.K));
  }

  /// <summary>Authored group membership (<c>IN_GROUP</c>) — an object may sit in several, nested or not.</summary>
  public void AddToGroup(BundleContainer group, int ord = 0) => _builder.Pipeline.InGroup(K, group.K, ord);

  // ── object → object ───────────────────────────────────────────────────────────────────────────────────

  /// <summary>The object this one is a component of (<c>SUBELEMENT</c>). Setting it appends to the parent.</summary>
  public BundleObject? Parent
  {
    get => _parent;
    set => value?.AddChild(this);
  }

  /// <summary>Declares <paramref name="child"/> a component of this object (<c>SUBELEMENT</c>).
  /// <paramref name="ord"/> is the child's position; null = next.</summary>
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

  /// <summary>Declares <paramref name="member"/> a member of this assembly (<c>IN_ASSEMBLY</c>): authored fabrication
  /// membership, separate from <see cref="AddChild"/> ownership. <paramref name="ord"/> is the member's position —
  /// 0 is the main member, ≥1 orders secondary or nested-assembly members; null = next. A member belongs to one
  /// assembly.</summary>
  public void AddAssemblyMember(BundleObject member, int? ord = null)
  {
    if (member.Assembly is not null)
    {
      if (ReferenceEquals(member.Assembly, this))
      {
        return;
      }
      throw new InvalidOperationException(
        $"Object '{member.ApplicationId}' is already a member of assembly '{member.Assembly.ApplicationId}'; a bundle edge cannot be retracted."
      );
    }
    int o = ord ?? _assemblyMemberOrd;
    _assemblyMemberOrd = Math.Max(_assemblyMemberOrd, o + 1);
    member.Assembly = this;
    _builder.Pipeline.InAssembly(member.K, K, o);
  }

  /// <summary>Host (<c>HOSTED_ON</c>): the wall a door is placed on. Not ownership.</summary>
  public BundleObject? Host
  {
    get => _host;
    set => Set(ref _host, value, v => _builder.Pipeline.HostedOn(K, v.K));
  }

  /// <summary>Containing room object (<c>IN_ROOM</c>).</summary>
  public BundleObject? Room
  {
    get => _room;
    set => Set(ref _room, value, v => _builder.Pipeline.InRoom(K, v.K, 0));
  }

  /// <summary>MEP connectivity (<c>CONNECTS_TO</c>); <paramref name="scope"/> is the producer's connection scope tag.</summary>
  public void ConnectTo(BundleObject other, int scope = 0) => _builder.Pipeline.ConnectsTo(K, other.K, scope);

  /// <summary>This object bounds <paramref name="room"/> (<c>BOUNDS</c>): a wall enclosing a room.</summary>
  public void Bounds(BundleObject room, int ord = 0) => _builder.Pipeline.Bounds(K, room.K, ord);

  public override string ToString() => Name is null ? ApplicationId : $"{Name} ({ApplicationId})";

  private static void Set<T>(ref T? field, T? value, Action<T> emit)
    where T : class
  {
    if (value is null || ReferenceEquals(field, value))
    {
      return;
    }
    if (field is not null)
    {
      // The edge is on disk already: pointing elsewhere cannot take it back, and letting it through would write a
      // second edge for a single-valued relation.
      throw new InvalidOperationException("This relation was already set; a bundle edge cannot be retracted.");
    }
    field = value;
    emit(value);
  }
}
