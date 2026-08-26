using System.Globalization;

namespace Speckle.Sdk.Pipelines.Receive.Artifacts;

/// <summary>A single decoded geometry payload from the <c>geometries</c> parquet: the raw blob plus its type tag
/// (<c>"SGEO"</c>-prefixed mesh blob, or a raw encoding like <c>rhino/3dm</c>).</summary>
#pragma warning disable CA1819 // raw geometry blob is intentionally a byte[]; this is a lightweight transport record
public readonly record struct ArtefactGeometry(byte[] Content, string Type)
#pragma warning restore CA1819
{
  /// <summary>True when the blob is a SGEO mesh blob (decode with <c>SgeoDecoder</c>), false for raw encodings (3dm).</summary>
  public bool IsSgeo =>
    Content.Length >= 4
    && Content[0] == (byte)'S'
    && Content[1] == (byte)'G'
    && Content[2] == (byte)'E'
    && Content[3] == (byte)'O';
}

/// <summary>One grouping tier of a scene view (the explorer/layer hierarchy). <see cref="Source"/> is <c>"rel"</c>
/// (group by a relation's target node, <see cref="Ref"/> = the <c>RelKind</c> number, e.g. 11=IN_MODEL, 7=ON_LEVEL,
/// 10=IN_COLLECTION) or <c>"eav"</c> (group by an object property, <see cref="Ref"/> = the property path, e.g.
/// <c>"category"</c>, <c>"family"</c>). Tiers are ordered outermost→innermost.</summary>
public readonly record struct SceneViewTier(string Source, string Ref);

/// <summary>A named camera viewpoint from <c>envelope.camera_views.parquet</c> (Rhino named view, Revit 3D view,
/// SketchUp scene). Position/target/ortho-height are in <see cref="Units"/> (model units); forward/up are unitless
/// UNIT vectors; <see cref="Fov"/> is the VERTICAL field of view in DEGREES (perspective only, null for ortho).</summary>
public sealed record ArtefactCameraView(
  int View,
  string? Name,
  bool IsDefault,
  int? Ord,
  double PosX,
  double PosY,
  double PosZ,
  double ForwardX,
  double ForwardY,
  double ForwardZ,
  double UpX,
  double UpY,
  double UpZ,
  double? TargetX,
  double? TargetY,
  double? TargetZ,
  string? Units,
  bool IsOrtho,
  double? Fov,
  double? LensMm,
  double? OrthoHeight,
  double? Aspect,
  double? Near,
  double? Far
);

/// <summary>An envelope graph node (Collection/Material/Definition/Instance/Level/…). Columns are sparse — only the
/// fields relevant to the node's <see cref="Kind"/> are populated.</summary>
public sealed record ArtefactNode(
  int Kind,
  string? Name,
  int? DefRef,
  string? Transform,
  string? Units,
  int? Argb,
  double? Opacity,
  double? Metalness,
  double? Roughness,
  double? Elevation,
  int? Emissive = null,
  double? Ior = null,
  string? GhTopology = null,
  string? Subtype = null
);

/// <summary>A relation edge in the envelope graph (<c>rel</c> = <see cref="RelKind"/>, <c>src</c>/<c>dst</c> dense ints).</summary>
public readonly record struct ArtefactEdge(int Src, int Dst, int Ord);

/// <summary>Envelope relations grouped by kind for direct lookup. The three dense-int namespaces are object
/// (<c>eav.object_index</c>), geometry (<c>geometryIndex</c>) and node (<c>nodes.id</c>); each relation maps between
/// two of them (e.g. DISPLAY src=object dst=geometry; IN_COLLECTION src=object dst=node).</summary>
public sealed class ArtefactRelations
{
  public List<ArtefactEdge> Display { get; } = new();
  public Dictionary<int, List<int>> SolidByObject { get; } = new();
  public Dictionary<int, int> CollectionByObject { get; } = new();

  /// <summary>IN_GROUP: object → CONTAINER(Group) nodes. MULTI-valued (unlike <see cref="CollectionByObject"/>,
  /// which last-wins because it IS the scene tree): groups are a separate, overlapping axis — an object keeps
  /// its collection and may sit in several (possibly nested) groups.</summary>
  public Dictionary<int, List<int>> GroupsByObject { get; } = new();

  /// <summary>SUBELEMENT (3): parent object → child object edges (ownership; ord = child order).</summary>
  public List<ArtefactEdge> Subelement { get; } = new();

  /// <summary>HOSTED_ON (22): hosted object → host object edges (a door placed on a wall — not ownership).</summary>
  public List<ArtefactEdge> HostedOn { get; } = new();

  /// <summary>CONNECTS_TO (21): source object → target object edges (MEP connectivity; ord = scope).</summary>
  public List<ArtefactEdge> ConnectsTo { get; } = new();

  /// <summary>BOUNDS (23): bounding object → room object edges (ord = boundary order).</summary>
  public List<ArtefactEdge> Bounds { get; } = new();

  /// <summary>IN_ROOM (12): object → containing room OBJECT (rooms are objects, not nodes).</summary>
  public List<ArtefactEdge> InRoom { get; } = new();

  /// <summary>IN_ASSEMBLY (18): member object → containing assembly object (ord = member order).</summary>
  public List<ArtefactEdge> InAssembly { get; } = new();

  /// <summary>DISPLAY_INSTANCE: object → INSTANCE node. Last-wins map (kept for the Base reconstruction path).</summary>
  public Dictionary<int, int> DisplayInstanceByObject { get; } = new();

  /// <summary>All DISPLAY_INSTANCE edges (object → INSTANCE node). An object may place several instances (e.g. a Revit
  /// railing → many balusters), so the native baker iterates these rather than the last-wins map above.</summary>
  public List<ArtefactEdge> DisplayInstanceEdges { get; } = new();

  /// <summary>For object→node relations (ON_LEVEL=7, IN_COLLECTION=10, IN_MODEL=11, IN_ROOM=12, …): rel → (object →
  /// target node). Used to resolve scene-view grouping tiers (e.g. an object's level/model/container) to a layer path.</summary>
  public Dictionary<int, Dictionary<int, int>> ObjectNodeByRel { get; } = new();
  public Dictionary<int, int> MaterialByGeometry { get; } = new();

  /// <summary>HAS_MATERIAL (<c>ord</c>=1): node(INSTANCE) → MATERIAL node — a material painted directly onto a
  /// block placement (e.g. Rhino's <c>MaterialFromObject</c> set on the instance itself), distinct from a
  /// geometry-sourced material. The geometry and instance-node K-spaces overlap numerically, so the edge's
  /// <c>ord</c> tags which one <c>src</c> belongs to — mirroring <see cref="ColorByObject"/>'s tag [ENG-8849].</summary>
  public Dictionary<int, int> MaterialByInstance { get; } = new();

  /// <summary>HAS_COLOR (<c>ord</c>=0): geometry → COLOR node. The object's by-object display colour (distinct from a
  /// render material); resolved back to the owning object via <see cref="ObjectByGeometry"/>, mirroring
  /// <see cref="MaterialByGeometry"/>.</summary>
  public Dictionary<int, int> ColorByGeometry { get; } = new();

  /// <summary>HAS_COLOR (<c>ord</c>=1): OBJECT → COLOR node — a colour carried by an object that owns no geometry of
  /// its own, i.e. a block/instance placement whose members render through its definition. Kept separate from
  /// <see cref="ColorByGeometry"/> because the two source namespaces overlap numerically; the edge's <c>ord</c> is
  /// the namespace tag (pre-tag bundles wrote 0, so they all land in ColorByGeometry as before) [ENG-8822].</summary>
  public Dictionary<int, int> ColorByObject { get; } = new();
  public Dictionary<int, List<int>> DefinesByDefinition { get; } = new();

  /// <summary>DEFINES ordinals, index-aligned with <see cref="DefinesByDefinition"/>. The ordinal is the member index
  /// (all geometry of one definition member shares it), so a consumer can group a member's authoritative solid + its
  /// display mesh(es) together and pick the solid over its shadow. Older bundles number per-geometry; a consumer that
  /// ignores this map just treats every geometry as its own member (the pre-grouping behaviour).</summary>
  public Dictionary<int, List<int>> DefinesOrdByDefinition { get; } = new();
  public Dictionary<int, List<int>> DefinesInstanceByDefinition { get; } = new();

  /// <summary>PLACES (24): definition-member object → its INSTANCE node. Association ONLY — never a render
  /// root (that is DISPLAY_INSTANCE). Ties a render-edge-less member object to its nested placement so its
  /// properties/IN_COLLECTION stay reachable; successor of the <c>@speckle.instance_k</c> eav stamp.</summary>
  public Dictionary<int, int> PlacesByObject { get; } = new();

  /// <summary>DEFINES_MEMBER (25): DEFINITION node → member objects, on the object plane where nothing is
  /// deduped. Ordinals (index-aligned in <see cref="MemberOrdByDefinition"/>) are MEMBER ordinals shared with
  /// <see cref="DefinesOrdByDefinition"/> — join (definition, ord) to recover a member's geometry unambiguously
  /// even when content-hash dedup shares one geometry K across definitions; successor of the
  /// <c>@speckle.geometry_k</c> eav stamp.</summary>
  public Dictionary<int, List<int>> MemberObjectsByDefinition { get; } = new();

  /// <summary>DEFINES_MEMBER ordinals, index-aligned with <see cref="MemberObjectsByDefinition"/>.</summary>
  public Dictionary<int, List<int>> MemberOrdByDefinition { get; } = new();

  /// <summary>OBJECT_HAS_MATERIAL (26): object → MATERIAL node — placement paint on the object plane
  /// (successor of the ord=1-tagged HAS_MATERIAL instance src). FILL semantics: geometry-level
  /// <see cref="MaterialByGeometry"/> always wins; this fills definition geometry with no material of its own,
  /// resolved down the placement chain (member object → placement via <see cref="PlacesByObject"/>).</summary>
  public Dictionary<int, int> MaterialByObject { get; } = new();

  /// <summary>NODE_HAS_MATERIAL (28): node → MATERIAL node — container appearance (e.g. a CONTAINER's
  /// authored material). Node-plane sibling of <see cref="MaterialByObject"/>.</summary>
  public Dictionary<int, int> MaterialByNode { get; } = new();

  /// <summary>NODE_HAS_COLOR (29): node → COLOR node — container display colour (e.g. a layer/tag colour
  /// as a first-class COLOR node rather than the CONTAINER argb overload).</summary>
  public Dictionary<int, int> ColorByNode { get; } = new();

  private Dictionary<int, List<ArtefactEdge>>? _displayByObject;

  /// <summary>The DISPLAY edges (object → mesh geometry) for one object, or null. Lazily indexed.</summary>
  public List<ArtefactEdge>? DisplayByObject(int objK)
  {
    _displayByObject ??= Display.GroupBy(e => e.Src).ToDictionary(g => g.Key, g => g.ToList());
    return _displayByObject.TryGetValue(objK, out var list) ? list : null;
  }

  /// <summary>Reverse of DISPLAY: mesh geometry index → owning object index (used to attribute HAS_MATERIAL/DEFINES,
  /// which target geometry, back to the owning object's applicationId).</summary>
  public Dictionary<int, int> ObjectByGeometry()
  {
    var map = new Dictionary<int, int>();
    foreach (var d in Display)
    {
      map[d.Dst] = d.Src;
    }
    return map;
  }

  internal void Add(Dictionary<int, List<int>> map, int key, int value)
  {
    if (!map.TryGetValue(key, out var list))
    {
      list = new List<int>();
      map[key] = list;
    }
    list.Add(value);
  }
}

/// <summary>One field row of an AEC/Civil3D property-set DEFINITION from the optional
/// <c>eav.property_set_definitions.parquet</c> — the set's SCHEMA only; values live per-object in eav under
/// <c>properties.Property Sets.{set}.{field}</c> and attachment is derived from those value paths.
/// <see cref="FieldBucketId"/> is THE rebind join key — the same string the value rows ship in
/// <c>eav.internal_definition_name</c> (null ⇒ match <see cref="FieldName"/> against the value path leaf);
/// <see cref="SetKey"/> is the definition's content hash (SET-level identity under same-name collisions).
/// List order is authored field order (row order in the file).</summary>
public sealed record ArtefactPropertySetField(
  string SetName,
  string SetKey,
  string? SetDescription,
  string FieldName,
  string? FieldBucketId,
  string? DataType,
  string? DefaultString,
  double? DefaultDouble,
  bool? DefaultBoolean,
  string? Unit,
  string? Description,
  string? AppliesTo
);

/// <summary>
/// The neutral, host-agnostic parse of a Speckle 4.0 artefact bundle (the directory of
/// <c>geometries</c>/<c>eav.*</c>/<c>envelope.*</c> parquet files produced by the send pipeline). Holds the dense-int
/// graph + raw geometry blobs without committing to any reconstructed <c>Base</c> shape — consumed both by the Rhino
/// direct-bake host builder and by <c>ObjectsArtifactReader</c> (which maps it to a <c>Base</c>/<c>Collection</c> graph
/// for the connectors that still go through the v1 host-build path, e.g. Revit). Geometry blobs are kept raw; SGEO
/// decoding (which needs <c>Speckle.Objects</c>) happens in the consumer.
/// </summary>
public sealed class ArtefactBundle
{
  public required Dictionary<int, ArtefactGeometry> Geometries { get; init; }
  public required Dictionary<int, string> ObjectAppIds { get; init; }
  public required Dictionary<int, Dictionary<string, object?>> Properties { get; init; }
  public required Dictionary<int, ArtefactNode> Nodes { get; init; }
  public required ArtefactRelations Relations { get; init; }
  public required string Units { get; init; }

  /// <summary>The default scene view's grouping tiers (outermost→innermost), or empty if the bundle has none. Drives
  /// the received layer hierarchy (e.g. Revit: Model → Level → Category → Family).</summary>
  public required IReadOnlyList<SceneViewTier> DefaultSceneView { get; init; }

  /// <summary>Named camera viewpoints (<c>envelope.camera_views.parquet</c>), ordered by <c>ord</c> then
  /// <c>view</c>; empty if the bundle ships none. Native bakers recreate them as host named views.</summary>
  public required IReadOnlyList<ArtefactCameraView> CameraViews { get; init; }

  /// <summary>MODEL/document-scoped attributes from the optional <c>eav.model.parquet</c> (object-less eav:
  /// project information, the full reference-point transform, document settings), nested by dotted path like
  /// <see cref="Properties"/>. Empty when the bundle ships no model file.</summary>
  public IReadOnlyDictionary<string, object?> ModelProperties { get; init; } = new Dictionary<string, object?>();

  /// <summary>AEC property-set definitions from the optional <c>eav.property_set_definitions.parquet</c>, one
  /// row per (set, field); empty when absent. Receivers recreate host set definitions from these, falling back
  /// to synthesizing minimal ones from the value rows when the file is missing.</summary>
  public IReadOnlyList<ArtefactPropertySetField> PropertySetDefinitions { get; init; } =
    Array.Empty<ArtefactPropertySetField>();

  /// <summary>Type-scoped properties (Revit Type Parameters / System Type Parameters, deduped once per type by
  /// <c>ObjectsArtifactPipeline.TrySplitTypeParameters</c>) resolved to each instance object that references a
  /// type via <c>eav.object_type</c> [ENG-9136]. Every object of the same type shares the SAME dictionary
  /// instance, so a model with many instances of few types parses each type's properties once, not per instance.
  /// Empty when the bundle ships no type tables (non-Revit sources, or bundles written before type splitting).</summary>
  public IReadOnlyDictionary<int, Dictionary<string, object?>> TypePropertiesByObject { get; init; } =
    new Dictionary<int, Dictionary<string, object?>>();
}

/// <summary>Reads the parquet files of an artefact bundle directory into a neutral <see cref="ArtefactBundle"/>.</summary>
public static class ArtefactBundleReader
{
  public static Task<ArtefactBundle> ReadAsync(string bundleDir, CancellationToken cancellationToken) =>
    ReadAsync(bundleDir, loadGeometry: true, cancellationToken);

  /// <summary>
  /// As <see cref="ReadAsync(string, CancellationToken)"/>; with <paramref name="loadGeometry"/> false the geometry
  /// shards are not opened and <see cref="ArtefactBundle.Geometries"/> comes back empty — load them later with
  /// <see cref="ReadGeometriesAsync"/>. Geometry is the bulk of a bundle, so a properties-only consumer saves most of
  /// the parse time and memory. One compat step is skipped in that mode: the pre-ENG-8822 untagged object-colour
  /// recovery, which needs to know which Ks are geometries.
  /// </summary>
  public static async Task<ArtefactBundle> ReadAsync(
    string bundleDir,
    bool loadGeometry,
    CancellationToken cancellationToken
  )
  {
    var geometriesTables = loadGeometry
      ? await ReadShardsAsync(bundleDir, cancellationToken).ConfigureAwait(false)
      : new List<ParquetTable>();
    var objectsT = await ReadTableAsync(bundleDir, ".eav.objects.parquet", cancellationToken).ConfigureAwait(false);
    var pathsT = await ReadTableAsync(bundleDir, ".eav.paths.parquet", cancellationToken).ConfigureAwait(false);
    var eavT = await ReadTableAsync(bundleDir, ".eav.eav.parquet", cancellationToken).ConfigureAwait(false);
    var nodesT = await ReadTableAsync(bundleDir, ".envelope.nodes.parquet", cancellationToken).ConfigureAwait(false);
    var relationsT = await ReadTableAsync(bundleDir, ".envelope.relations.parquet", cancellationToken)
      .ConfigureAwait(false);
    var sceneViewsT = await TryReadTableAsync(bundleDir, ".envelope.scene_views.parquet", cancellationToken)
      .ConfigureAwait(false);
    var cameraViewsT = await TryReadTableAsync(bundleDir, ".envelope.camera_views.parquet", cancellationToken)
      .ConfigureAwait(false);
    // Type-scoped params (ENG-9136) — additive tables, absent from bundles predating type splitting.
    var typeEavT = await TryReadTableAsync(bundleDir, ".eav.type_eav.parquet", cancellationToken).ConfigureAwait(false);
    var objectTypeT = await TryReadTableAsync(bundleDir, ".eav.object_type.parquet", cancellationToken)
      .ConfigureAwait(false);
    // Optional purpose files — absent from bundles predating them (feature-detect by presence).
    var modelT = await TryReadTableAsync(bundleDir, ".eav.model.parquet", cancellationToken).ConfigureAwait(false);
    var psetDefsT = await TryReadTableAsync(bundleDir, ".eav.property_set_definitions.parquet", cancellationToken)
      .ConfigureAwait(false);

    var objIdToApp = BuildObjectIds(objectsT);
    var pathById = BuildPaths(pathsT);
    var propsByObject = BuildProperties(eavT, pathById, "object_index");
    var geometries = LoadGeometries(geometriesTables);
    var relations = LoadRelations(relationsT);
    if (loadGeometry)
    {
      RecoverUntaggedObjectColors(relations, geometries, objIdToApp);
    }

    return new ArtefactBundle
    {
      Geometries = geometries,
      ObjectAppIds = objIdToApp,
      Properties = propsByObject,
      Nodes = LoadNodes(nodesT),
      Relations = relations,
      Units = InferUnits(propsByObject),
      DefaultSceneView = LoadDefaultSceneView(sceneViewsT),
      CameraViews = LoadCameraViews(cameraViewsT),
      TypePropertiesByObject = LoadTypeProperties(typeEavT, objectTypeT, pathById),
      ModelProperties = LoadModelProperties(modelT),
      PropertySetDefinitions = LoadPropertySetDefinitions(psetDefsT),
    };
  }

  // ENG-9136: type_eav has the identical (index, path_index, value_*) row shape as eav, keyed by type_index
  // instead of object_index — BuildProperties parses it the same way. object_type is the weak ref (object_index →
  // type_index); joining it here means every object of one type shares the SAME parsed dictionary instance, so a
  // model with many instances of few types costs one parse per type, not one per instance.
  private static Dictionary<int, Dictionary<string, object?>> LoadTypeProperties(
    ParquetTable? typeEavT,
    ParquetTable? objectTypeT,
    Dictionary<int, string> pathById
  )
  {
    var byObject = new Dictionary<int, Dictionary<string, object?>>();
    if (typeEavT is null || objectTypeT is null || !objectTypeT.Has("object_index"))
    {
      return byObject;
    }
    var propsByType = BuildProperties(typeEavT, pathById, "type_index");
    var objIdx = objectTypeT.Ints("object_index");
    var typeIdx = objectTypeT.Ints("type_index");
    for (int i = 0; i < objIdx.Length; i++)
    {
      if (propsByType.TryGetValue(typeIdx[i], out var props))
      {
        byObject[objIdx[i]] = props;
      }
    }
    return byObject;
  }

  // Compat for bundles written before the ord namespace tag (ENG-8822): an object-sourced HAS_COLOR edge landed in
  // ColorByGeometry with ord=0, indistinguishable from a geometry-sourced one. Recover it ONLY when the geometry
  // reading is provably impossible — the src is no geometry K but IS an object K — so a tagged or colliding bundle
  // is never second-guessed. Untagged edges whose K collides with a real geometry stay unrecovered (they'd be a
  // coin flip); re-send with a current producer to tag them.
  private static void RecoverUntaggedObjectColors(
    ArtefactRelations relations,
    Dictionary<int, ArtefactGeometry> geometries,
    Dictionary<int, string> objectAppIds
  )
  {
    List<int>? recovered = null;
    foreach (var kv in relations.ColorByGeometry)
    {
      if (!geometries.ContainsKey(kv.Key) && objectAppIds.ContainsKey(kv.Key))
      {
        (recovered ??= new List<int>()).Add(kv.Key);
      }
    }
    if (recovered is null)
    {
      return;
    }
    foreach (var k in recovered)
    {
      relations.ColorByObject[k] = relations.ColorByGeometry[k];
      relations.ColorByGeometry.Remove(k);
    }
  }

  // Model-scoped attributes (object-less eav): same coalesce as BuildProperties, path inlined per row.
  private static Dictionary<string, object?> LoadModelProperties(ParquetTable? t)
  {
    var dict = new Dictionary<string, object?>();
    if (t is null || !t.Has("path"))
    {
      return dict;
    }
    var path = t.Strings("path");
    var vStr = t.Strings("value_string");
    var vDbl = t.NullableDoubles("value_double");
    var vBool = t.NullableBools("value_boolean");
    for (int i = 0; i < path.Length; i++)
    {
      object? value =
        vBool[i].HasValue ? vBool[i]
        : vDbl[i].HasValue ? vDbl[i]
        : vStr[i];
      if (value is null || path[i] is not { Length: > 0 } p)
      {
        continue;
      }
      SetNested(dict, p, value);
    }
    return dict;
  }

  private static IReadOnlyList<ArtefactPropertySetField> LoadPropertySetDefinitions(ParquetTable? t)
  {
    if (t is null || !t.Has("set_name"))
    {
      return Array.Empty<ArtefactPropertySetField>();
    }
    var setName = t.Strings("set_name");
    var setKey = t.Strings("set_key");
    var setDescription = t.Strings("set_description");
    var fieldName = t.Strings("field_name");
    var fieldBucketId = t.Strings("field_bucket_id");
    var dataType = t.Strings("data_type");
    var defStr = t.Strings("default_string");
    var defDbl = t.NullableDoubles("default_double");
    var defBool = t.NullableBools("default_boolean");
    var unit = t.Strings("unit");
    var description = t.Strings("description");
    var appliesTo = t.Strings("applies_to");
    var rows = new List<ArtefactPropertySetField>(setName.Length);
    for (int i = 0; i < setName.Length; i++)
    {
      rows.Add(
        new ArtefactPropertySetField(
          setName[i] ?? "",
          setKey[i] ?? "",
          setDescription[i],
          fieldName[i] ?? "",
          fieldBucketId[i],
          dataType[i],
          defStr[i],
          defDbl[i],
          defBool[i],
          unit[i],
          description[i],
          appliesTo[i]
        )
      );
    }
    return rows;
  }

  // ENG-8947: the reference-point provision from meta (single row). Columns are nullable + additive — older
  // bundles without them yield (null, null), i.e. internal origin.
  // object→node relations (per envelope rel_types) whose target node can form a scene-view grouping tier.
  private static readonly HashSet<int> s_objectNodeRels = new()
  {
    RelKind.OnLevel,
    RelKind.InCollection,
    RelKind.InModel,
    12,
    13,
    14,
    15,
    16,
    RelKind.InGroup,
    18,
    19,
    20, // IN_ROOM/IN_SPACE/IN_SYSTEM/IN_NETWORK/IN_LINE/IN_GROUP/IN_ASSEMBLY/…/XREF
  };

  private static IReadOnlyList<SceneViewTier> LoadDefaultSceneView(ParquetTable? t)
  {
    if (t is null || !t.Has("source"))
    {
      return Array.Empty<SceneViewTier>();
    }
    var isDefault = t.NullableBools("is_default");
    var ord = t.Ints("ord");
    var source = t.Strings("source");
    var refs = t.Strings("ref");
    var tiers = new List<(int Ord, SceneViewTier Tier)>();
    for (int i = 0; i < source.Length; i++)
    {
      if (isDefault[i] != true)
      {
        continue;
      }
      tiers.Add((ord[i], new SceneViewTier(source[i] ?? "", refs[i] ?? "")));
    }
    return tiers.OrderBy(x => x.Ord).Select(x => x.Tier).ToList();
  }

  private static IReadOnlyList<ArtefactCameraView> LoadCameraViews(ParquetTable? t)
  {
    if (t is null || !t.Has("pos_x"))
    {
      return Array.Empty<ArtefactCameraView>();
    }
    var view = t.Ints("view");
    var name = t.Strings("name");
    var isDefault = t.NullableBools("is_default");
    var ord = t.NullableInts("ord");
    var posX = t.NullableDoubles("pos_x");
    var posY = t.NullableDoubles("pos_y");
    var posZ = t.NullableDoubles("pos_z");
    var fwdX = t.NullableDoubles("forward_x");
    var fwdY = t.NullableDoubles("forward_y");
    var fwdZ = t.NullableDoubles("forward_z");
    var upX = t.NullableDoubles("up_x");
    var upY = t.NullableDoubles("up_y");
    var upZ = t.NullableDoubles("up_z");
    var tgtX = t.NullableDoubles("target_x");
    var tgtY = t.NullableDoubles("target_y");
    var tgtZ = t.NullableDoubles("target_z");
    var units = t.Strings("units");
    var isOrtho = t.NullableBools("is_ortho");
    var fov = t.NullableDoubles("fov");
    var lensMm = t.NullableDoubles("lens_mm");
    var orthoHeight = t.NullableDoubles("ortho_height");
    var aspect = t.NullableDoubles("aspect");
    var near = t.NullableDoubles("near");
    var far = t.NullableDoubles("far");
    var views = new List<ArtefactCameraView>(view.Length);
    for (int i = 0; i < view.Length; i++)
    {
      views.Add(
        new ArtefactCameraView(
          view[i],
          name[i],
          isDefault[i] ?? false,
          ord[i],
          posX[i] ?? 0,
          posY[i] ?? 0,
          posZ[i] ?? 0,
          fwdX[i] ?? 0,
          fwdY[i] ?? 0,
          fwdZ[i] ?? 0,
          upX[i] ?? 0,
          upY[i] ?? 0,
          upZ[i] ?? 1,
          tgtX[i],
          tgtY[i],
          tgtZ[i],
          units[i],
          isOrtho[i] ?? false,
          fov[i],
          lensMm[i],
          orthoHeight[i],
          aspect[i],
          near[i],
          far[i]
        )
      );
    }
    return views.OrderBy(v => v.Ord ?? int.MaxValue).ThenBy(v => v.View).ToList();
  }

  /// <summary>Reads only the geometry shards (<c>*.geometries*.parquet</c>) of a bundle directory, keyed by dense
  /// geometry index. The deferred half of <see cref="ReadAsync(string, bool, CancellationToken)"/>.</summary>
  /// <exception cref="FileNotFoundException">The directory holds no geometry shard.</exception>
  public static async Task<Dictionary<int, ArtefactGeometry>> ReadGeometriesAsync(
    string bundleDir,
    CancellationToken cancellationToken
  ) => LoadGeometries(await ReadShardsAsync(bundleDir, cancellationToken).ConfigureAwait(false));

  private static Dictionary<int, ArtefactGeometry> LoadGeometries(List<ParquetTable> tables)
  {
    var map = new Dictionary<int, ArtefactGeometry>();
    foreach (var t in tables)
    {
      var idx = t.Ints("geometryIndex");
      var content = t.Blobs("content");
      var type = t.Strings("type");
      for (int i = 0; i < idx.Length; i++)
      {
        var bytes = content[i];
        if (bytes is null)
        {
          continue;
        }
        map[idx[i]] = new ArtefactGeometry(bytes, type[i] ?? "");
      }
    }
    return map;
  }

  private static Dictionary<int, string> BuildObjectIds(ParquetTable t)
  {
    var map = new Dictionary<int, string>();
    var idx = t.Ints("object_index");
    var app = t.Strings("application_id");
    for (int i = 0; i < idx.Length; i++)
    {
      map[idx[i]] = app[i] ?? idx[i].ToString(CultureInfo.InvariantCulture);
    }
    return map;
  }

  private static Dictionary<int, string> BuildPaths(ParquetTable t)
  {
    var map = new Dictionary<int, string>();
    var idx = t.Ints("path_index");
    var path = t.Strings("path");
    for (int i = 0; i < idx.Length; i++)
    {
      map[idx[i]] = path[i] ?? "";
    }
    return map;
  }

  // keyColumn is "object_index" for the instance-scoped eav table, or "type_index" for the type-scoped
  // type_eav table (ENG-9136) — both share the identical (key, path_index, value_*) row shape.
  private static Dictionary<int, Dictionary<string, object?>> BuildProperties(
    ParquetTable t,
    Dictionary<int, string> pathById,
    string keyColumn
  )
  {
    var byKey = new Dictionary<int, Dictionary<string, object?>>();
    if (!t.Has(keyColumn))
    {
      return byKey;
    }
    var keyIdx = t.Ints(keyColumn);
    var pathIdx = t.Ints("path_index");
    var vStr = t.Strings("value_string");
    var vDbl = t.NullableDoubles("value_double");
    var vBool = t.NullableBools("value_boolean");
    for (int i = 0; i < keyIdx.Length; i++)
    {
      object? value =
        vBool[i].HasValue ? vBool[i]
        : vDbl[i].HasValue ? vDbl[i]
        : vStr[i];
      if (value is null)
      {
        continue;
      }
      string path = pathById.TryGetValue(pathIdx[i], out var p) ? p : "";
      if (path.Length == 0)
      {
        continue;
      }
      if (!byKey.TryGetValue(keyIdx[i], out var dict))
      {
        dict = new Dictionary<string, object?>();
        byKey[keyIdx[i]] = dict;
      }
      SetNested(dict, path, value);
    }
    return byKey;
  }

  private static void SetNested(Dictionary<string, object?> root, string path, object? value)
  {
    var parts = path.Split('.');
    var cursor = root;
    for (int i = 0; i < parts.Length - 1; i++)
    {
      if (cursor.TryGetValue(parts[i], out var next) && next is Dictionary<string, object?> nd)
      {
        cursor = nd;
      }
      else
      {
        var nd2 = new Dictionary<string, object?>();
        cursor[parts[i]] = nd2;
        cursor = nd2;
      }
    }
    cursor[parts[^1]] = value;
  }

  private static string InferUnits(Dictionary<int, Dictionary<string, object?>> propsByObject)
  {
    foreach (var p in propsByObject.Values)
    {
      if (p.TryGetValue("units", out var v) && v is string s && s.Length > 0)
      {
        return s;
      }
    }
    return "none";
  }

  private static Dictionary<int, ArtefactNode> LoadNodes(ParquetTable t)
  {
    var map = new Dictionary<int, ArtefactNode>();
    var id = t.Ints("id");
    var kind = t.Ints("kind");
    var name = t.Strings("name");
    var defRef = t.NullableInts("def_ref");
    var transform = t.Strings("transform");
    var units = t.Strings("units");
    var argb = t.NullableInts("argb");
    var opacity = t.NullableDoubles("opacity");
    var metalness = t.NullableDoubles("metalness");
    var roughness = t.NullableDoubles("roughness");
    var elevation = t.NullableDoubles("elevation");
    // emissive/ior joined the nodes table later [ENG-8791] — absent from older bundles, so guard with Has().
    var emissive = t.Has("emissive") ? t.NullableInts("emissive") : null;
    var ior = t.Has("ior") ? t.NullableDoubles("ior") : null;
    // gh_topology joined later too — same Has() guard for bundles written before it existed.
    var ghTopology = t.Has("gh_topology") ? t.Strings("gh_topology") : null;
    // subtype (CONTAINER flavour: Layer / Category / Group / Model …) — Has() guard for the same reason.
    var subtype = t.Has("subtype") ? t.Strings("subtype") : null;
    for (int i = 0; i < id.Length; i++)
    {
      map[id[i]] = new ArtefactNode(
        kind[i],
        name[i],
        defRef[i],
        transform[i],
        units[i],
        argb[i],
        opacity[i],
        metalness[i],
        roughness[i],
        elevation[i],
        emissive?[i],
        ior?[i],
        ghTopology?[i],
        subtype?[i]
      );
    }
    return map;
  }

  private static ArtefactRelations LoadRelations(ParquetTable t)
  {
    var sets = new ArtefactRelations();
    var rel = t.Ints("rel");
    var src = t.Ints("src");
    var dst = t.Ints("dst");
    var ord = t.Ints("ord");
    for (int i = 0; i < rel.Length; i++)
    {
      switch (rel[i])
      {
        case RelKind.Display:
          sets.Display.Add(new ArtefactEdge(src[i], dst[i], ord[i]));
          break;
        case RelKind.Solid:
          sets.Add(sets.SolidByObject, src[i], dst[i]);
          break;
        case RelKind.InCollection:
          sets.CollectionByObject[src[i]] = dst[i];
          break;
        case RelKind.InGroup:
          sets.Add(sets.GroupsByObject, src[i], dst[i]);
          break;
        case RelKind.DisplayInstance:
          sets.DisplayInstanceByObject[src[i]] = dst[i];
          sets.DisplayInstanceEdges.Add(new ArtefactEdge(src[i], dst[i], ord[i]));
          break;
        case RelKind.HasMaterial:
          // ord tags the src namespace: 1 = INSTANCE node (a placement-painted material), 0/absent = geometry
          // [ENG-8849], mirroring HAS_COLOR's ord tag.
          if (ord[i] == 1)
          {
            sets.MaterialByInstance[src[i]] = dst[i];
          }
          else
          {
            sets.MaterialByGeometry[src[i]] = dst[i];
          }
          break;
        case RelKind.HasColor:
          // ord tags the src namespace: 1 = object (instance placement), 0/absent = geometry [ENG-8822].
          if (ord[i] == 1)
          {
            sets.ColorByObject[src[i]] = dst[i];
          }
          else
          {
            sets.ColorByGeometry[src[i]] = dst[i];
          }
          break;
        case RelKind.Defines:
          sets.Add(sets.DefinesByDefinition, src[i], dst[i]);
          sets.Add(sets.DefinesOrdByDefinition, src[i], ord[i]);
          break;
        case RelKind.DefinesInstance:
          sets.Add(sets.DefinesInstanceByDefinition, src[i], dst[i]);
          break;
        case RelKind.Places:
          sets.PlacesByObject[src[i]] = dst[i];
          break;
        case RelKind.Subelement:
          sets.Subelement.Add(new ArtefactEdge(src[i], dst[i], ord[i]));
          break;
        case RelKind.HostedOn:
          sets.HostedOn.Add(new ArtefactEdge(src[i], dst[i], ord[i]));
          break;
        case RelKind.ConnectsTo:
          sets.ConnectsTo.Add(new ArtefactEdge(src[i], dst[i], ord[i]));
          break;
        case RelKind.Bounds:
          sets.Bounds.Add(new ArtefactEdge(src[i], dst[i], ord[i]));
          break;
        case RelKind.InRoom:
          sets.InRoom.Add(new ArtefactEdge(src[i], dst[i], ord[i]));
          break;
        case RelKind.InAssembly:
          sets.InAssembly.Add(new ArtefactEdge(src[i], dst[i], ord[i]));
          break;
        case RelKind.DefinesMember:
          sets.Add(sets.MemberObjectsByDefinition, src[i], dst[i]);
          sets.Add(sets.MemberOrdByDefinition, src[i], ord[i]);
          break;
        case RelKind.ObjectHasMaterial:
          sets.MaterialByObject[src[i]] = dst[i];
          break;
        case RelKind.ObjectHasColor:
          // Same consumer home as the legacy ord=1-tagged HAS_COLOR object src — one map, two vintages.
          sets.ColorByObject[src[i]] = dst[i];
          break;
        case RelKind.NodeHasMaterial:
          sets.MaterialByNode[src[i]] = dst[i];
          break;
        case RelKind.NodeHasColor:
          sets.ColorByNode[src[i]] = dst[i];
          break;
        default:
          break;
      }

      // object→node relations (ON_LEVEL/IN_COLLECTION/IN_MODEL/IN_ROOM/…) drive scene-view grouping tiers.
      if (s_objectNodeRels.Contains(rel[i]))
      {
        if (!sets.ObjectNodeByRel.TryGetValue(rel[i], out var map))
        {
          map = new Dictionary<int, int>();
          sets.ObjectNodeByRel[rel[i]] = map;
        }
        map[src[i]] = dst[i];
      }
    }
    return sets;
  }

  private static async Task<ParquetTable> ReadTableAsync(string dir, string suffix, CancellationToken ct)
  {
    var path =
      Directory.EnumerateFiles(dir, "*.parquet").FirstOrDefault(p => p.EndsWith(suffix, StringComparison.Ordinal))
      ?? throw new FileNotFoundException($"Artefact table '*{suffix}' not found in bundle '{dir}'.");
    return await ParquetTableReader.ReadAsync(path, ct).ConfigureAwait(false);
  }

  // Optional table (older bundles may lack e.g. scene_views) → null when absent.
  private static async Task<ParquetTable?> TryReadTableAsync(string dir, string suffix, CancellationToken ct)
  {
    var path = Directory
      .EnumerateFiles(dir, "*.parquet")
      .FirstOrDefault(p => p.EndsWith(suffix, StringComparison.Ordinal));
    return path is null ? null : await ParquetTableReader.ReadAsync(path, ct).ConfigureAwait(false);
  }

  private static async Task<List<ParquetTable>> ReadShardsAsync(string dir, CancellationToken ct)
  {
    var tables = new List<ParquetTable>();
    foreach (var path in Directory.EnumerateFiles(dir, "*.geometries*.parquet").OrderBy(p => p, StringComparer.Ordinal))
    {
      tables.Add(await ParquetTableReader.ReadAsync(path, ct).ConfigureAwait(false));
    }
    if (tables.Count == 0)
    {
      throw new FileNotFoundException($"Artefact geometries '*.geometries*.parquet' not found in bundle '{dir}'.");
    }
    return tables;
  }
}
