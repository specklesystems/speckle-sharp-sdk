using Speckle.Sdk.Pipelines;
using Speckle.Sdk.Pipelines.Receive.Artifacts;

namespace Speckle.Sdk.Bundles;

/// <summary>
/// One object of a received <see cref="Model"/>: a property-carrying entity identified by its host
/// <see cref="ApplicationId"/>. Properties are exposed flat, keyed by the dotted path the bundle stores them under
/// (<c>"Constraints.Base Offset"</c>, <c>"Identity Data.Mark"</c>). Relationships are accessors — object→object ones
/// return <see cref="ModelObject"/>s, object→node ones return <see cref="ModelNode"/>s — so they chain:
/// <c>door.Host?.Level?.Name</c>.
/// </summary>
public sealed class ModelObject
{
  // Producers write an object's user-facing properties under this root; root-level scalars (name, units, …) sit
  // beside it. The façade drops the root so scripts address "Constraints.Base Offset", not "properties.Constraints…".
  internal const string PROPERTIES_ROOT = "properties";
  internal const string PROPERTIES_PREFIX = "properties.";
  private const int NESTING_GUARD = 32;

  private readonly Model _model;
  private readonly Lazy<IReadOnlyList<string>> _collectionPath;
  private readonly Lazy<IReadOnlyList<ModelGeometry>> _geometries;

  internal ModelObject(Model model, int k, string applicationId)
  {
    _model = model;
    K = k;
    ApplicationId = applicationId;
    _collectionPath = new(() => ResolveCollectionPath(model.Bundle, k));
    _geometries = new(() => ResolveGeometries(model, this));
  }

  private int TypeK => _model.Bundle.TypeIndexByObject.TryGetValue(K, out int t) ? t : -1;

  /// <summary>Dense object index inside the bundle — the key into <see cref="Model.Bundle"/>'s object-keyed maps.</summary>
  public int K { get; }

  /// <summary>The host application's id for this object. The only identity a bundle object has.</summary>
  public string ApplicationId { get; }

  /// <summary>The object's root <c>name</c>, if the producer stamped one.</summary>
  public string? Name => _model.Properties_.GetString(K, "name");

  // ── properties ────────────────────────────────────────────────────────────────────────────────────────
  // Views over the bundle's PropertyTable row range for this object: no dictionaries are built unless enumerated.

  /// <summary>Instance-level properties, flat and path-keyed (without the <c>properties.</c> root).</summary>
  public IReadOnlyDictionary<string, object?> Properties => _model.Properties_.Under(K, PROPERTIES_ROOT);

  /// <summary>Every property row of this object as stored — root scalars (<c>name</c>, <c>units</c>, …) and the
  /// <c>properties.</c>-prefixed ones together.</summary>
  public IReadOnlyDictionary<string, object?> RootProperties => _model.Properties_[K];

  /// <summary>Type-level properties (family/type/definition parameters) resolved for this object, flat and path-keyed.
  /// Empty when the producer wrote no type tables.</summary>
  public IReadOnlyDictionary<string, object?> TypeProperties => _model.Bundle.TypeProperties(K).Under(PROPERTIES_ROOT);

  /// <summary>Property lookup by dotted path. Instance properties win over type properties, then root scalars; null when absent.</summary>
  public object? this[string path] =>
    _model.Properties_.TryGetValue(K, PROPERTIES_PREFIX + path, out var v) ? v
    : TypeK >= 0 && _model.TypeProperties_.TryGetValue(TypeK, PROPERTIES_PREFIX + path, out var t) ? t
    : _model.Properties_.TryGetValue(K, path, out var r) ? r
    : null;

  /// <summary>Unboxed typed lookups over the same precedence as the indexer; null when absent or of another type.</summary>
  public double? GetDouble(string path) =>
    _model.Properties_.GetDouble(K, PROPERTIES_PREFIX + path)
    ?? (TypeK >= 0 ? _model.TypeProperties_.GetDouble(TypeK, PROPERTIES_PREFIX + path) : null)
    ?? _model.Properties_.GetDouble(K, path);

  public string? GetString(string path) =>
    _model.Properties_.GetString(K, PROPERTIES_PREFIX + path)
    ?? (TypeK >= 0 ? _model.TypeProperties_.GetString(TypeK, PROPERTIES_PREFIX + path) : null)
    ?? _model.Properties_.GetString(K, path);

  public bool? GetBool(string path) =>
    _model.Properties_.GetBool(K, PROPERTIES_PREFIX + path)
    ?? (TypeK >= 0 ? _model.TypeProperties_.GetBool(TypeK, PROPERTIES_PREFIX + path) : null)
    ?? _model.Properties_.GetBool(K, path);

  // ── geometry ──────────────────────────────────────────────────────────────────────────────────────────

  /// <summary>
  /// The object's geometries in draw order: direct <c>DISPLAY</c> meshes and <c>SOLID</c> blobs, plus — for placed
  /// objects (<c>DISPLAY_INSTANCE</c>) — the definition's geometry carrying the placement <see cref="ModelGeometry.Transform"/>,
  /// nested placements included. Resolved on first access; triggers <see cref="Model.Geometries"/> (the bundle-wide
  /// shard parse) the first time any object asks. Empty for property-only objects.
  /// </summary>
  public IReadOnlyList<ModelGeometry> Geometries => _geometries.Value;

  // ── object → object ───────────────────────────────────────────────────────────────────────────────────

  /// <summary>Owning object (<c>SUBELEMENT</c>): the element this one is a component of. Null for top-level objects.</summary>
  public ModelObject? Parent => _model.Index.ParentByChild.TryGetValue(K, out int p) ? _model.Object(p) : null;

  /// <summary>Component objects (<c>SUBELEMENT</c>), in ordinal order.</summary>
  public IReadOnlyList<ModelObject> Children => _model.ObjectsFor(_model.Index.Get(_model.Index.ChildrenByParent, K));

  /// <summary>The object this one is placed on (<c>HOSTED_ON</c>: door → wall, fixture → ceiling). Not ownership — see <see cref="Parent"/>.</summary>
  public ModelObject? Host => _model.Index.HostByHosted.TryGetValue(K, out int h) ? _model.Object(h) : null;

  /// <summary>Objects placed on this one (<c>HOSTED_ON</c> reversed).</summary>
  public IReadOnlyList<ModelObject> Hosted => _model.ObjectsFor(_model.Index.Get(_model.Index.HostedByHost, K));

  /// <summary>Objects this one connects to (<c>CONNECTS_TO</c>, MEP connectivity), either direction.</summary>
  public IReadOnlyList<ModelObject> ConnectedTo => _model.ObjectsFor(_model.Index.Get(_model.Index.Connections, K));

  /// <summary>Room objects this object bounds (<c>BOUNDS</c>): for a wall, the rooms it encloses.</summary>
  public IReadOnlyList<ModelObject> BoundsRooms => _model.ObjectsFor(_model.Index.Get(_model.Index.RoomsByBounding, K));

  /// <summary>Objects bounding this one (<c>BOUNDS</c> reversed): for a room, its boundary elements in order.</summary>
  public IReadOnlyList<ModelObject> BoundedBy => _model.ObjectsFor(_model.Index.Get(_model.Index.BoundingByRoom, K));

  /// <summary>The room this object sits in (<c>IN_ROOM</c>: furniture/door/window → room). Rooms are objects.</summary>
  public ModelObject? Room => _model.Index.RoomByObject.TryGetValue(K, out int r) ? _model.Object(r) : null;

  /// <summary>Objects located in this room (<c>IN_ROOM</c> reversed). Empty unless this object is a room.</summary>
  public IReadOnlyList<ModelObject> Contains => _model.ObjectsFor(_model.Index.Get(_model.Index.ObjectsByRoom, K));

  /// <summary>The fabrication assembly this object is a member of (<c>IN_ASSEMBLY</c>, Tekla).</summary>
  public ModelObject? Assembly => _model.Index.AssemblyByMember.TryGetValue(K, out int a) ? _model.Object(a) : null;

  /// <summary>Member objects of this assembly (<c>IN_ASSEMBLY</c> reversed), in member order.</summary>
  public IReadOnlyList<ModelObject> AssemblyMembers =>
    _model.ObjectsFor(_model.Index.Get(_model.Index.MembersByAssembly, K));

  // ── object → node ─────────────────────────────────────────────────────────────────────────────────────

  /// <summary>Level (<c>ON_LEVEL</c>), with <see cref="ModelLevel.Elevation"/>.</summary>
  public ModelLevel? Level => NodeByRel(RelKind.OnLevel) as ModelLevel;

  /// <summary>MEP system container (<c>IN_SYSTEM</c>).</summary>
  public ModelContainer? System => NodeByRel(RelKind.InSystem) as ModelContainer;

  /// <summary>Direct scene-tree container (<c>IN_COLLECTION</c>). <see cref="CollectionPath"/> is its name path.</summary>
  public ModelContainer? Collection =>
    _model.Bundle.Relations.CollectionByObject.TryGetValue(K, out int c) ? _model.Node(c) as ModelContainer : null;

  /// <summary>Groups this object sits in (<c>IN_GROUP</c>) — an overlapping axis to <see cref="Collection"/>, possibly several.</summary>
  public IReadOnlyList<ModelContainer> Groups =>
    _model.NodesFor<ModelContainer>(_model.Bundle.Relations.GroupsByObject.TryGetValue(K, out var g) ? g : null);

  /// <summary>Grouping path, outermost first (e.g. <c>["Level 1", "Walls"]</c>): the bundle's default scene view when it
  /// declares one, otherwise the object's <c>IN_COLLECTION</c> ancestry. <see cref="SceneViewSegments"/> is the same
  /// path with the node behind each segment.</summary>
  public IReadOnlyList<string> CollectionPath => _collectionPath.Value;

  /// <summary>The object's position in the default scene view (<see cref="Model.DefaultSceneView"/>), outermost first:
  /// relation tiers resolve to the node (Model container, level, layer …), property-value tiers to a name only.
  /// Falls back to the <c>IN_COLLECTION</c> ancestry when the bundle declares no scene view.</summary>
  public IReadOnlyList<ModelSceneViewSegment> SceneViewSegments
  {
    get
    {
      var bundle = _model.Bundle;
      var segments = new List<ModelSceneViewSegment>();
      if (bundle.DefaultSceneView.Count > 0)
      {
        foreach (var (name, _, nodeK) in SceneViewResolver.SegmentsWithAppearance(bundle, K))
        {
          segments.Add(new ModelSceneViewSegment(name, _model.NodeOrNull(nodeK)));
        }
        return segments;
      }
      for (var c = Collection; c is not null; c = c.Parent)
      {
        segments.Insert(0, new ModelSceneViewSegment(c.Name ?? "", c));
      }
      return segments;
    }
  }

  /// <summary>Nearest container material (<c>NODE_HAS_MATERIAL</c>), walking <see cref="Collection"/> upwards.</summary>
  internal ModelMaterial? ContainerMaterial
  {
    get
    {
      for (var c = Collection; c is not null; c = c.Parent)
      {
        if (c.Material is { } m)
        {
          return m;
        }
      }
      return null;
    }
  }

  /// <summary>Nearest container colour (<c>NODE_HAS_COLOR</c>), walking <see cref="Collection"/> upwards.</summary>
  internal ModelColor? ContainerColor
  {
    get
    {
      for (var c = Collection; c is not null; c = c.Parent)
      {
        if (c.Color is { } col)
        {
          return col;
        }
      }
      return null;
    }
  }

  /// <summary>Material on the object plane (<c>OBJECT_HAS_MATERIAL</c>): applies to all the object's geometry unless a
  /// geometry carries its own (<see cref="ModelGeometry.Material"/>).</summary>
  public ModelMaterial? Material =>
    _model.Bundle.Relations.MaterialByObject.TryGetValue(K, out int m) ? _model.Node(m) as ModelMaterial : null;

  /// <summary>Colour on the object plane (<c>OBJECT_HAS_COLOR</c>, or a legacy object-tagged <c>HAS_COLOR</c>).</summary>
  public ModelColor? Color =>
    _model.Bundle.Relations.ColorByObject.TryGetValue(K, out int c) ? _model.Node(c) as ModelColor : null;

  // ── instancing ────────────────────────────────────────────────────────────────────────────────────────

  /// <summary>INSTANCE nodes this object renders through (<c>DISPLAY_INSTANCE</c>), or — for a definition member —
  /// the placement it belongs to (<c>PLACES</c>). Empty for directly-rendered objects.</summary>
  public IReadOnlyList<ModelInstance> Placements
  {
    get
    {
      var direct = _model.Index.Get(_model.Index.InstancesByObject, K);
      if (direct is { Count: > 0 })
      {
        return _model.NodesFor<ModelInstance>(direct);
      }
      return _model.Bundle.Relations.PlacesByObject.TryGetValue(K, out int p)
        ? _model.NodesFor<ModelInstance>([p])
        : Array.Empty<ModelInstance>();
    }
  }

  /// <summary>The definitions this object renders through, distinct, in placement order. Usually one; a Revit railing
  /// with baluster placements has several. Empty when not instanced.</summary>
  public IReadOnlyList<ModelDefinition> Definitions
  {
    get
    {
      var placements = Placements;
      if (placements.Count == 0)
      {
        return Array.Empty<ModelDefinition>();
      }
      var list = new List<ModelDefinition>();
      foreach (var placement in placements)
      {
        if (placement.Definition is { } d && !list.Contains(d))
        {
          list.Add(d);
        }
      }
      return list;
    }
  }

  /// <summary>The first of <see cref="Definitions"/>, or null when not instanced.</summary>
  public ModelDefinition? Definition => Definitions.Count > 0 ? Definitions[0] : null;

  public override string ToString() => Name is null ? ApplicationId : $"{Name} ({ApplicationId})";

  private ModelNode? NodeByRel(int rel) =>
    _model.Bundle.Relations.ObjectNodeByRel.TryGetValue(rel, out var map) && map.TryGetValue(K, out int n)
      ? _model.Node(n)
      : null;

  // ── resolvers ─────────────────────────────────────────────────────────────────────────────────────────

  private static IReadOnlyList<ModelGeometry> ResolveGeometries(Model model, ModelObject owner)
  {
    int k = owner.K;
    var rels = model.Bundle.Relations;
    var result = new List<ModelGeometry>();

    var placements = model.Index.Get(model.Index.InstancesByObject, k);
    bool hasDirect = rels.DisplayByObject(k) is { Count: > 0 } || rels.SolidByObject.ContainsKey(k);
    if (!hasDirect && placements is null)
    {
      return result;
    }

    var geometries = model.Geometries; // one bundle-wide parse, shared by every object

    if (rels.DisplayByObject(k) is { } display)
    {
      foreach (var e in display)
      {
        if (geometries.TryGetValue(e.Dst, out var g))
        {
          result.Add(
            new ModelGeometry(model, owner, e.Dst, g, GeometryRole.Display, e.Ord, transform: null, instanceK: null)
          );
        }
      }
    }
    if (rels.SolidByObject.TryGetValue(k, out var solids))
    {
      for (int i = 0; i < solids.Count; i++)
      {
        if (geometries.TryGetValue(solids[i], out var g))
        {
          result.Add(
            new ModelGeometry(model, owner, solids[i], g, GeometryRole.Solid, i, transform: null, instanceK: null)
          );
        }
      }
    }
    if (placements is not null)
    {
      foreach (var e in rels.DisplayInstanceEdges)
      {
        if (e.Src == k)
        {
          AddPlacement(model, owner, e.Dst, e.Ord, parentTransform: null, result, depth: 0);
        }
      }
    }

    result.Sort((a, b) => a.Ord.CompareTo(b.Ord));
    return result;
  }

  // INSTANCE node → its DEFINITION's geometry (and nested INSTANCEs), composing transforms outer→inner.
  private static void AddPlacement(
    Model model,
    ModelObject owner,
    int instanceK,
    int ord,
    IReadOnlyList<double>? parentTransform,
    List<ModelGeometry> into,
    int depth
  )
  {
    if (
      depth > NESTING_GUARD
      || !model.Bundle.Nodes.TryGetValue(instanceK, out var instance)
      || instance.DefRef is null
    )
    {
      return;
    }
    var transform = Compose(parentTransform, ModelInstance.ParseTransform(instance.Transform));
    var rels = model.Bundle.Relations;
    int definitionK = instance.DefRef.Value;

    if (rels.DefinesByDefinition.TryGetValue(definitionK, out var geometryKs))
    {
      var ords = rels.DefinesOrdByDefinition.TryGetValue(definitionK, out var o) ? o : null;
      for (int i = 0; i < geometryKs.Count; i++)
      {
        if (model.Geometries.TryGetValue(geometryKs[i], out var g))
        {
          into.Add(
            new ModelGeometry(
              model,
              owner,
              geometryKs[i],
              g,
              GeometryRole.Display,
              ords is null ? ord : ords[i],
              transform,
              instanceK
            )
          );
        }
      }
    }
    if (rels.DefinesInstanceByDefinition.TryGetValue(definitionK, out var nested))
    {
      foreach (int nestedK in nested)
      {
        AddPlacement(model, owner, nestedK, ord, transform, into, depth + 1);
      }
    }
  }

  // Row-major 4×4 product parent·child, in the bundle's stored order.
  private static IReadOnlyList<double>? Compose(IReadOnlyList<double>? parent, IReadOnlyList<double>? child)
  {
    if (parent is null)
    {
      return child;
    }
    if (child is null)
    {
      return parent;
    }
    var r = new double[16];
    for (int row = 0; row < 4; row++)
    {
      for (int col = 0; col < 4; col++)
      {
        double sum = 0;
        for (int i = 0; i < 4; i++)
        {
          sum += parent[row * 4 + i] * child[i * 4 + col];
        }
        r[row * 4 + col] = sum;
      }
    }
    return r;
  }

  private static IReadOnlyList<string> ResolveCollectionPath(ArtefactBundle bundle, int k)
  {
    var fromView = SceneViewResolver.Segments(bundle, k);
    if (fromView.Count > 0 || !bundle.Relations.CollectionByObject.TryGetValue(k, out int collectionK))
    {
      return fromView;
    }
    return SceneViewResolver.NodeAncestry(bundle.Nodes, collectionK);
  }
}
