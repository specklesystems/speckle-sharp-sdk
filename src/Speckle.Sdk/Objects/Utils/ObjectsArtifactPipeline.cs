#if NETSTANDARD2_0 || NET8_0_OR_GREATER
using System.Globalization;
using Speckle.Sdk;
using Speckle.Sdk.Models;
using Speckle.Sdk.Pipelines;
using Speckle.Sdk.Pipelines.Send.Artifacts;

namespace Speckle.Objects.Utils;

/// <summary>
/// Speckle 4.0 producer for the artefact bundle (see <c>notes/topology-envelope-SOT.md</c>).
/// PARQUET-ONLY, one file per table, no DuckDB: <c>geometries.parquet</c> (SGEO blobs,
/// <see cref="GeometriesParquetWriter"/>), the <c>envelope.*.parquet</c> topology graph
/// (<see cref="EnvelopeWriter"/>), and the <c>eav.*.parquet</c> tables (<see cref="EavWriter"/>).
/// Owns the three per-namespace identity interners (object / geometry / node) and exposes a typed
/// emit API so the producer stays string-based while the artefacts store dense <c>int32</c>. The
/// object namespace is interned by <see cref="EavWriter.GetOrAddObject"/> (eav is the dictionary home).
/// Producing is decoupled from uploading: write here, then hand the paths to the uploader.
/// </summary>
public sealed class ObjectsArtifactPipeline : IDisposable
{
  // One background writer thread shared by all three artefacts. Interning/buffering is synchronous on the
  // ODA extraction thread; every row-group flush + file finalize is handed to this scheduler, so Parquet's
  // sync-over-async IO never runs on the ODA pinned thread (no deadlock) and is bounded by its queue.
  private readonly ParquetWriteScheduler _scheduler = new();
  private readonly GeometriesParquetWriter _geometriesWriter;
  private readonly EnvelopeWriter _envelopeWriter;
  private readonly EavWriter _eavWriter;
  private readonly ISet<string> _excludedProperties;
  private readonly string _outputDir;
  private readonly string _baseName;

  // Lazily created on the first AddStructuralResult: the structural_results purpose file is OPTIONAL and
  // feature-detected by file presence, so a bundle with no results must ship NO file — not an empty table
  // polluting every non-structural connector's bundle catalog.
  private StructuralResultsWriter? _structuralResultsWriter;

  // Same lazy-optional contract as structural_results: no rows → no file.
  private ModelEavWriter? _modelEavWriter;
  private PropertySetDefinitionsWriter? _propertySetDefinitionsWriter;

  // Per-namespace interners. The object namespace is owned by the eav writer (it writes the
  // dictionary), so it is not duplicated here.
  private readonly IdInterner _geometryInterner = new();
  private readonly IdInterner _nodeInterner = new();

  public ObjectsArtifactPipeline(
    string outputDir,
    string baseName,
    ISpeckleApplication producer,
    ISet<string>? excludedTopLevelProperties = null
  )
  {
    _geometriesWriter = new GeometriesParquetWriter(outputDir, baseName, _scheduler);
    _envelopeWriter = new EnvelopeWriter(outputDir, baseName, _scheduler);
    SetProducer(producer);
    _eavWriter = new EavWriter(outputDir, baseName, _scheduler);
    _outputDir = outputDir;
    _baseName = baseName;
    _excludedProperties = excludedTopLevelProperties ?? EavExtraction.DefaultExcludedTopLevelProperties;
  }

  public string GeometriesPath => _geometriesWriter.GeometriesPath;

  /// <summary>Output directory of the <c>envelope.*.parquet</c> tables. Name kept (<c>...DbPath</c>)
  /// for caller compatibility — no DuckDB is written.</summary>
  public string EnvelopeDbPath => _envelopeWriter.EnvelopeDbPath;

  /// <summary>Output directory of the <c>eav.*.parquet</c> tables. Name kept (<c>...DbPath</c>)
  /// for caller compatibility — no DuckDB is written.</summary>
  public string EavDbPath => _eavWriter.EavDbPath;

  /// <summary>Interns <paramref name="applicationId"/> via the eav dictionary to its dense <c>K</c>, so
  /// the caller emits envelope edges with the SAME id eav uses.</summary>
  public int InternObject(string applicationId) => _eavWriter.GetOrAddObject(applicationId);

  /// <summary>Flattens an object's property tree into <c>eav</c>. <paramref name="properties"/> is the
  /// merged-ancestry dictionary (geometry excluded); <paramref name="rootScalars"/> are bare top-level
  /// labels (speckle_type, name, units, …).</summary>
  public void AddProperties(
    string applicationId,
    IReadOnlyDictionary<string, object?> properties,
    IEnumerable<KeyValuePair<string, object?>>? rootScalars = null,
    string? typeKey = null
  )
  {
    // No type key (or nothing type-scoped) → flatten everything per-object.
    if (typeKey is null || !TrySplitTypeParameters(properties, out var instanceProps, out var typeSubtree))
    {
      var rows = new List<EavRow>();
      EavExtraction.FlattenProperties(applicationId, properties, rootScalars, _excludedProperties, rows);
      _eavWriter.AddRows(applicationId, rows);
      return;
    }

    // Instance-scoped props → eav; Type/System params deduped into type_eav (flattened once per type,
    // via the lazy factory) with an object_type weak ref. See notes/topology-envelope-SOT.md §6.
    var instanceRows = new List<EavRow>();
    EavExtraction.FlattenProperties(applicationId, instanceProps, rootScalars, _excludedProperties, instanceRows);
    _eavWriter.AddRows(applicationId, instanceRows);

    _eavWriter.AddType(
      applicationId,
      typeKey,
      () =>
      {
        var typeRows = new List<EavRow>();
        EavExtraction.FlattenSubtree(typeSubtree, "properties.Parameters", typeRows);
        return typeRows;
      }
    );
  }

  // Splits `properties.Parameters` into instance-scoped (kept on the object) and type-scoped (Type +
  // System Parameters, deduped per type). False if there's nothing type-scoped to split out.
  private static bool TrySplitTypeParameters(
    IReadOnlyDictionary<string, object?> properties,
    out IReadOnlyDictionary<string, object?> instanceProps,
    out IReadOnlyDictionary<string, object?> typeSubtree
  )
  {
    instanceProps = properties;
    typeSubtree = s_emptyDict;

    if (!properties.TryGetValue("Parameters", out var pv) || pv is not IReadOnlyDictionary<string, object?> paramsDict)
    {
      return false;
    }

    var typeParams = new Dictionary<string, object?>(StringComparer.Ordinal);
    var instanceParams = new Dictionary<string, object?>(StringComparer.Ordinal);
    foreach (var kv in paramsDict)
    {
      if (kv.Key is "Type Parameters" or "System Type Parameters")
      {
        typeParams[kv.Key] = kv.Value;
      }
      else
      {
        instanceParams[kv.Key] = kv.Value;
      }
    }

    if (typeParams.Count == 0)
    {
      return false;
    }

    // Copy via foreach (the Dictionary(IEnumerable<KVP>, comparer) ctor is net5+; netstandard2.0 only has the
    // IDictionary ctor, and `properties` is an IReadOnlyDictionary).
    var merged = new Dictionary<string, object?>(StringComparer.Ordinal);
    foreach (var kv in properties)
    {
      merged[kv.Key] = kv.Value;
    }
    merged["Parameters"] = instanceParams;
    instanceProps = merged;
    typeSubtree = typeParams;
    return true;
  }

  private static readonly IReadOnlyDictionary<string, object?> s_emptyDict = new Dictionary<string, object?>();

  /// <summary>Interns <paramref name="meshApplicationId"/> to a dense geometry <c>K</c>, encoding +
  /// storing the SGEO blob on first sight (skipped on repeats), and returns the <c>K</c>.</summary>
  public int AddGeometry(string meshApplicationId, Base geometry)
  {
    if (_geometryInterner.GetOrAdd(meshApplicationId, out var geometryK))
    {
      _geometriesWriter.AddGeometry(geometryK, SgeoEncoder.Encode(geometry));
    }
    return geometryK;
  }

  /// <summary>Interns <paramref name="geometryApplicationId"/> to a dense geometry <c>K</c> and stores the
  /// RAW <paramref name="content"/> bytes verbatim (no SGEO encoding) with a <paramref name="type"/> label
  /// on first sight. For host-native geometry kept losslessly for receive — e.g. a Rhino Brep serialized to
  /// a 3dm blob (<c>type = "3dm"</c>) linked via the <c>SOLID</c> rel.</summary>
  public int AddRawGeometry(string geometryApplicationId, byte[] content, string type)
  {
    if (_geometryInterner.GetOrAdd(geometryApplicationId, out var geometryK))
    {
      _geometriesWriter.AddRawGeometry(geometryK, content, type);
    }
    return geometryK;
  }

  /// <summary>Resolves the geometry <c>K</c> for an already-added mesh (lookup, no encode) — for post-loop
  /// <c>DEFINES</c>/<c>HAS_MATERIAL</c> edges referencing meshes by host applicationId.</summary>
  public int InternGeometryId(string meshApplicationId) => _geometryInterner.GetOrAdd(meshApplicationId);

  /// <summary>Interns a DEFINITION node (instance-definition / block), writing it once.</summary>
  public int AddDefinition(string definitionKey, string? name)
  {
    if (_nodeInterner.GetOrAdd("def:" + definitionKey, out var k))
    {
      _envelopeWriter.AddNode(
        k,
        NodeKind.Definition,
        name,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null
      );
    }
    return k;
  }

  /// <summary>Interns an INSTANCE (placement) node — its transform + the DEFINITION it
  /// references. <paramref name="transform"/> is 16 row-major doubles.</summary>
  public int AddInstance(string placementKey, int defRef, IReadOnlyList<double> transform, string? units)
  {
    if (_nodeInterner.GetOrAdd("inst:" + placementKey, out var k))
    {
      _envelopeWriter.AddNode(
        k,
        NodeKind.Instance,
        null,
        defRef,
        FormatTransform(transform),
        units,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null
      );
    }
    return k;
  }

  /// <summary>Interns a MATERIAL value-node (inline render value), writing it once. <paramref name="name"/> is the
  /// authored material name (Rhino/Revit/AutoCAD material table entry) carried in the shared node <c>name</c> column
  /// so receivers can recreate the host material under its original name instead of a colour-derived placeholder;
  /// null = unnamed. It is NOT part of the intern key — dedup stays keyed on <paramref name="materialKey"/>.
  /// <paramref name="emissive"/> is the packed ARGB emissive colour — NULL is the canonical "no emission" in the
  /// bundle and consumers default it to black; a black RGB is normalized to NULL here, so producers may pass
  /// <c>RenderMaterial.emissive</c> (black default) naively. <paramref name="ior"/> is the index of refraction
  /// (null = the host has no IOR concept) — together the remaining universal PBR scalars [ENG-8791].</summary>
  public int AddMaterial(
    string materialKey,
    string? name,
    int argb,
    double opacity,
    double metalness,
    double roughness,
    int? emissive = null,
    double? ior = null
  )
  {
    // Black emission IS "no emission" (the alpha byte is meaningless on an emissive colour) — normalize to NULL so
    // the column has one spelling of "off" and null-RLEs away, regardless of which producer sent it.
    if (emissive is int e && (e & 0xFFFFFF) == 0)
    {
      emissive = null;
    }
    if (_nodeInterner.GetOrAdd("mat:" + materialKey, out var k))
    {
      _envelopeWriter.AddNode(
        k,
        NodeKind.Material,
        name,
        null,
        null,
        null,
        null,
        argb,
        opacity,
        metalness,
        roughness,
        emissive,
        ior,
        null
      );
    }
    return k;
  }

  /// <summary>Interns a COLOR value-node (keyed by its argb), writing it once.</summary>
  public int AddColor(int argb)
  {
    if (_nodeInterner.GetOrAdd("col:" + argb.ToString(CultureInfo.InvariantCulture), out var k))
    {
      _envelopeWriter.AddNode(
        k,
        NodeKind.Color,
        null,
        null,
        null,
        null,
        null,
        argb,
        null,
        null,
        null,
        null,
        null,
        null
      );
    }
    return k;
  }

  /// <summary>Interns a LEVEL value-node (name + elevation), writing it once.</summary>
  public int AddLevel(string levelKey, string? name, double elevation)
  {
    if (_nodeInterner.GetOrAdd("lvl:" + levelKey, out var k))
    {
      _envelopeWriter.AddNode(
        k,
        NodeKind.Level,
        name,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        elevation
      );
    }
    return k;
  }

  /// <summary>Interns a scene-tree collection (layer / category / story) node, once. A collection is a
  /// CONTAINER node whose <c>subtype</c> carries its tag; <c>IN_COLLECTION</c> marks the grouping axis.
  /// <paramref name="parentCollectionK"/> is the parent collection (null = top-level) — the parent chain IS
  /// the source hierarchy. <paramref name="subtype"/> tags it (e.g. "Layer") for the loader.</summary>
  public int AddCollection(
    string collectionKey,
    string? name,
    int? parentCollectionK,
    string? subtype,
    string? ghTopology = null
  )
  {
    if (_nodeInterner.GetOrAdd("coll:" + collectionKey, out var k))
    {
      // ghTopology carries Grasshopper's data-tree paths for this collection
      // (nodes.gh_topology) so a tree survives a round trip; null for every other producer.
      _envelopeWriter.AddNode(
        k,
        NodeKind.Container,
        name,
        parentCollectionK,
        null,
        null,
        subtype,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        ghTopology
      );
    }
    return k;
  }

  /// <summary>Interns a CONTAINER (semantic-topology bucket: model / room / system / …) node, once. Distinct
  /// from <see cref="AddCollection"/> (authored scene-tree). <paramref name="parentContainerK"/> is its parent
  /// CONTAINER (null = top-level; self-nesting for nested links). <paramref name="subtype"/> is the canonical
  /// axis tag (e.g. "Model") — use the SAME tag across connectors for the same concept.</summary>
  public int AddContainer(string containerKey, string? name, int? parentContainerK, string? subtype)
  {
    if (_nodeInterner.GetOrAdd("cont:" + containerKey, out var k))
    {
      _envelopeWriter.AddNode(
        k,
        NodeKind.Container,
        name,
        parentContainerK,
        null,
        null,
        subtype,
        null,
        null,
        null,
        null,
        null,
        null,
        null
      );
    }
    return k;
  }

  // ── relations ──────────────────────────────────────────────────────────────────────

  /// <summary>object → geometry: direct renderable geometry (world-coord mesh).</summary>
  public void Display(int objectK, int geometryK, int ord) =>
    _envelopeWriter.AddRelation(RelKind.Display, objectK, geometryK, ord);

  /// <summary>object → node(INSTANCE): renderable via a placement (transform + definition).</summary>
  public void DisplayInstance(int objectK, int instanceK, int ord) =>
    _envelopeWriter.AddRelation(RelKind.DisplayInstance, objectK, instanceK, ord);

  /// <summary>object → geometry: authoritative solid.</summary>
  public void Solid(int objectK, int geometryK, int ord) =>
    _envelopeWriter.AddRelation(RelKind.Solid, objectK, geometryK, ord);

  /// <summary>object → object: host→hosted (curtain wall → panel).</summary>
  public void Subelement(int parentObjectK, int childObjectK, int ord) =>
    _envelopeWriter.AddRelation(RelKind.Subelement, parentObjectK, childObjectK, ord);

  /// <summary>node(DEFINITION) → geometry: definition contains a raw mesh member.</summary>
  public void Defines(int definitionK, int geometryK, int ord) =>
    _envelopeWriter.AddRelation(RelKind.Defines, definitionK, geometryK, ord);

  /// <summary>node(DEFINITION) → node(nested INSTANCE): definition contains a nested block placement.
  /// Distinct rel from <see cref="Defines"/> so <c>rel</c> fixes the dst namespace (node, not geometry).</summary>
  public void DefinesInstance(int definitionK, int instanceK, int ord) =>
    _envelopeWriter.AddRelation(RelKind.DefinesInstance, definitionK, instanceK, ord);

  /// <summary>geometry | node(INSTANCE) → node(MATERIAL): per-mesh render material, or (<paramref name="srcIsInstance"/>)
  /// a material painted directly onto a block placement that owns no geometry of its own. The geometry and
  /// instance-node K-spaces overlap numerically, so <paramref name="srcIsInstance"/> tags which one
  /// <paramref name="srcK"/> belongs to in the edge's <c>ord</c> column — mirroring <see cref="HasColor"/>'s
  /// <c>srcIsObject</c> tag [ENG-8849].</summary>
  public void HasMaterial(int srcK, int materialK, bool srcIsInstance = false) =>
    _envelopeWriter.AddRelation(RelKind.HasMaterial, srcK, materialK, srcIsInstance ? 1 : 0);

  /// <summary>geometry → node(COLOR): display colour.</summary>
  public void HasColor(int srcK, int colorK) => _envelopeWriter.AddRelation(RelKind.HasColor, srcK, colorK, 0);

  /// <summary>object → node(LEVEL): level membership.</summary>
  public void OnLevel(int objectK, int levelK) => _envelopeWriter.AddRelation(RelKind.OnLevel, objectK, levelK, 0);

  /// <summary>object → node(COLLECTION): the object's direct membership in a scene-tree container.</summary>
  public void InCollection(int objectK, int collectionK, int ord) =>
    _envelopeWriter.AddRelation(RelKind.InCollection, objectK, collectionK, ord);

  /// <summary>object → node(CONTAINER, subtype "Model"): the object's source-document / host / linked-model
  /// membership. The default-projection top key for Revit links / Navis federated files (SOT §8).</summary>
  public void InModel(int objectK, int modelK, int ord) =>
    _envelopeWriter.AddRelation(RelKind.InModel, objectK, modelK, ord);

  /// <summary>object → object: spatial occupancy — the object's containing ROOM object. Rooms are sent as
  /// objects (geometry = room volume), so <paramref name="roomK"/> is the room's interned object K, not a node.</summary>
  public void InRoom(int objectK, int roomK, int ord) =>
    _envelopeWriter.AddRelation(RelKind.InRoom, objectK, roomK, ord);

  /// <summary>object → node(CONTAINER, subtype "System"): logical engineering-system membership (Revit
  /// MEPSystem, IFC IfcDistributionSystem). Also home of NETWORKS — a CONTAINER with subtype "Network"
  /// reached through this same rel (IN_NETWORK was collapsed into IN_SYSTEM).</summary>
  public void InSystem(int objectK, int systemK, int ord) =>
    _envelopeWriter.AddRelation(RelKind.InSystem, objectK, systemK, ord);

  /// <summary>object → node(CONTAINER, subtype "Group"): authored scene-group membership (Rhino/AutoCAD
  /// groups). A SEPARATE axis from <see cref="InCollection"/> (the layer/collection scene-tree, single-valued
  /// on receive): an object keeps its collection AND its group(s); groups may nest (container parent chain)
  /// and overlap, so an object may carry several IN_GROUP edges.</summary>
  public void InGroup(int objectK, int groupK, int ord) =>
    _envelopeWriter.AddRelation(RelKind.InGroup, objectK, groupK, ord);

  /// <summary>object → object: physical flow connectivity, DIRECTED src→dst by flow (source→target). A
  /// reciprocal pair encodes undirected / unknown flow. Unscoped (ord=0) — see the scoped overload.</summary>
  public void ConnectsTo(int sourceObjectK, int targetObjectK) => ConnectsTo(sourceObjectK, targetObjectK, 0);

  /// <summary>object → object connectivity with a SCOPE tag: CONNECTS_TO uses <c>ord</c> as a scope, not an
  /// ordinal (<c>rel_types.ord_semantics='scope'</c>). <paramref name="scope"/> is an MEP system K (flow), a
  /// door/opening K (room adjacency), or 0 — so one table carries several overlapping connectivity graphs.</summary>
  public void ConnectsTo(int sourceObjectK, int targetObjectK, int scope) =>
    _envelopeWriter.AddRelation(RelKind.ConnectsTo, sourceObjectK, targetObjectK, scope);

  /// <summary>object → object: hosted element → its HOST (door/window → wall, fixture → ceiling/floor/face).
  /// A DIFFERENT semantic from <see cref="Subelement"/> ownership: a hosted element is PLACED ON its host, not
  /// a component of it. Precedence matches the producers: a valid owner wins (SUBELEMENT), hosting is the
  /// fallback. Emit only when BOTH endpoints are sent objects (no dangling edges). Un-retired post-v5 (ENG-8867).</summary>
  public void HostedOn(int hostedObjectK, int hostObjectK) =>
    _envelopeWriter.AddRelation(RelKind.HostedOn, hostedObjectK, hostObjectK, 0);

  /// <summary>object → object: a room-bounding element → the ROOM object it bounds (which walls/separators form a
  /// room's footprint, for egress / plan analysis). Both are interned objects.</summary>
  public void Bounds(int boundingObjectK, int roomObjectK, int ord) =>
    _envelopeWriter.AddRelation(RelKind.Bounds, boundingObjectK, roomObjectK, ord);

  /// <summary>object(member) → object(assembly): authored fabrication membership (<c>IN_ASSEMBLY</c>, rel 18), separate
  /// from <see cref="Subelement"/> ownership — a Tekla/CSi assembly groups members it does not own. <paramref name="ord"/>
  /// is the member's position: 0 is the main member, ≥1 orders secondary or nested-assembly members. Both endpoints
  /// are interned objects. Direction and ordinal semantics match specklepy's <c>in_assembly</c>.</summary>
  public void InAssembly(int memberObjectK, int assemblyObjectK, int ord) =>
    _envelopeWriter.AddRelation(RelKind.InAssembly, memberObjectK, assemblyObjectK, ord);

  /// <summary>object(definition member) → node(INSTANCE): the association-only object↔placement map (rel 24
  /// PLACES). Ties a render-edge-less definition-member object to its nested placement so its properties and
  /// <see cref="InCollection"/> membership stay reachable — replaces the <c>@speckle.instance_k</c> eav stamp.
  /// NEVER a render root: that is <see cref="DisplayInstance"/>, whose every edge is drawn in world space.</summary>
  public void Places(int memberObjectK, int instanceK) =>
    _envelopeWriter.AddRelation(RelKind.Places, memberObjectK, instanceK, 0);

  /// <summary>node(DEFINITION) → object(member): definition membership on the OBJECT plane, where nothing is
  /// deduped (rel 25 DEFINES_MEMBER). <paramref name="memberOrd"/> is the MEMBER ordinal shared with the
  /// member's <see cref="Defines"/> rows — joining (definition, ord) recovers each member's geometry even when
  /// content-hash dedup collapses identical meshes across definitions. Replaces the <c>@speckle.geometry_k</c>
  /// eav stamp; instance-members join via <see cref="Places"/> instead.</summary>
  public void DefinesMember(int definitionK, int memberObjectK, int memberOrd) =>
    _envelopeWriter.AddRelation(RelKind.DefinesMember, definitionK, memberObjectK, memberOrd);

  /// <summary>object → node(MATERIAL): placement paint on the object plane (rel 26, successor of
  /// <see cref="HasMaterial"/> with <c>srcIsInstance</c>). FILL semantics: geometry-level HAS_MATERIAL always
  /// wins; the object's material fills definition geometry with no material of its own, resolved down the
  /// placement chain (a nested member object reaches its placement via <see cref="Places"/>).</summary>
  public void ObjectHasMaterial(int objectK, int materialK) =>
    _envelopeWriter.AddRelation(RelKind.ObjectHasMaterial, objectK, materialK, 0);

  /// <summary>object → node(COLOR): object-plane colour (rel 27, successor of <see cref="HasColor"/> with
  /// <c>srcIsObject</c>). OVERRIDE semantics — deliberately the INVERSE of <see cref="ObjectHasMaterial"/>:
  /// material is intrinsic (geometry owns, object fills), colour is presentational (object OVERRIDES,
  /// geometry-level HAS_COLOR is the default) [spec #16].</summary>
  public void ObjectHasColor(int objectK, int colorK) =>
    _envelopeWriter.AddRelation(RelKind.ObjectHasColor, objectK, colorK, 0);

  /// <summary>node → node(MATERIAL): container appearance (rel 28, NODE_HAS_MATERIAL) — e.g. a
  /// CONTAINER's authored material.</summary>
  public void NodeHasMaterial(int nodeK, int materialK) =>
    _envelopeWriter.AddRelation(RelKind.NodeHasMaterial, nodeK, materialK, 0);

  /// <summary>node → node(COLOR): container display colour (rel 29, NODE_HAS_COLOR) — e.g. a layer/tag
  /// colour as a first-class COLOR node instead of the CONTAINER argb overload.</summary>
  public void NodeHasColor(int nodeK, int colorK) =>
    _envelopeWriter.AddRelation(RelKind.NodeHasColor, nodeK, colorK, 0);

  // ── structural results ─────────────────────────────────────────────────────────────────

  /// <summary>
  /// Appends one structural analysis/design result value to <c>{base}.eav.structural_results.parquet</c>
  /// (see <see cref="StructuralResultsWriter"/>). <b>Object-level</b> results pass the member/joint's
  /// <paramref name="objectApplicationId"/> (resolved to the SAME dense K the object was interned with, so
  /// results join back to it) and leave <paramref name="location"/> null; <b>group-level</b> results
  /// (pier/spandrel forces) pass <paramref name="elementName"/> — a named group of walls, NOT an interned
  /// object — with <paramref name="location"/> = story; <b>model/story-level</b> results (story drift,
  /// modal period, base reaction) pass neither and identify via <paramref name="location"/> (story) and/or
  /// <paramref name="step"/> (mode). <paramref name="positionLabel"/> is a categorical position/direction
  /// (Top/Bottom, X/Y) — distinct from the numeric member <paramref name="station"/>. Numeric results set
  /// <paramref name="value"/>; non-numeric design verdicts set <paramref name="valueText"/>.
  /// </summary>
  public void AddStructuralResult(
    string? objectApplicationId,
    string? location,
    string resultType,
    string loadCase,
    string component,
    double? station,
    int? step,
    double? value,
    string? valueText = null,
    string? elementName = null,
    string? positionLabel = null
  )
  {
    int? objectIndex = objectApplicationId is null ? null : _eavWriter.GetOrAddObject(objectApplicationId);
    _structuralResultsWriter ??= new StructuralResultsWriter(_outputDir, _baseName, _scheduler);
    _structuralResultsWriter.AddRow(
      objectIndex,
      elementName,
      location,
      resultType,
      loadCase,
      component,
      positionLabel,
      station,
      step,
      value,
      valueText
    );
  }

  // ── model / property-set definitions (optional purpose files) ──────────────────────────

  /// <summary>Appends one MODEL/document-scoped attribute to the optional <c>{base}.eav.model.parquet</c>
  /// (see <see cref="ModelEavWriter"/>) — facts of the model itself (project information, the full
  /// reference-point transform, document settings) that have no owning object. <paramref name="value"/> is
  /// coalesced into exactly one typed column (bool / numeric / string); null values write no row. A bundle
  /// where this is never called ships no file.</summary>
  public void AddModelProperty(string path, object? value, string? unit = null)
  {
    if (string.IsNullOrEmpty(path) || value is null)
    {
      return;
    }
    string? s = null;
    double? d = null;
    bool? b = null;
    switch (value)
    {
      case bool bv:
        b = bv;
        break;
      case string sv:
        s = sv;
        break;
      case sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal:
        d = Convert.ToDouble(value, CultureInfo.InvariantCulture);
        break;
      default:
        s = Convert.ToString(value, CultureInfo.InvariantCulture);
        break;
    }
    if (d is { } dv && (double.IsNaN(dv) || double.IsInfinity(dv)))
    {
      return; // eav convention: finite numerics only
    }
    if (s is null && d is null && b is null)
    {
      return;
    }
    _modelEavWriter ??= new ModelEavWriter(_outputDir, _baseName, _scheduler);
    _modelEavWriter.AddRow(path, s, d, b, unit);
  }

  /// <summary>Appends one field row of an AEC/Civil3D property-set DEFINITION to the optional
  /// <c>{base}.eav.property_set_definitions.parquet</c> (see <see cref="PropertySetDefinitionsWriter"/>) —
  /// the schema only; values stay per-object in eav under <c>properties.Property Sets.{set}.{field}</c> and
  /// attachment is derived from those value paths. <paramref name="setKey"/> is the definition's content hash
  /// (SET-level identity); <paramref name="fieldBucketId"/> is THE rebind join key — the same string the value
  /// rows ship in <c>eav.internal_definition_name</c> (null ⇒ consumers match <paramref name="fieldName"/>
  /// against the value path leaf). Call once per field, in authored field order (row order is field order).</summary>
  public void AddPropertySetDefinition(
    string setName,
    string setKey,
    string fieldName,
    string? fieldBucketId,
    string? dataType,
    string? defaultString = null,
    double? defaultDouble = null,
    bool? defaultBoolean = null,
    string? unit = null,
    string? description = null,
    string? setDescription = null,
    string? appliesTo = null
  )
  {
    _propertySetDefinitionsWriter ??= new PropertySetDefinitionsWriter(_outputDir, _baseName, _scheduler);
    _propertySetDefinitionsWriter.AddRow(
      setName,
      setKey,
      setDescription,
      fieldName,
      fieldBucketId,
      dataType,
      defaultString,
      defaultDouble,
      defaultBoolean,
      unit,
      description,
      appliesTo
    );
  }

  /// <summary>Authors a scene_views projection (SOT §8): the default (+ optional named) scene-explorer
  /// grouping the consumer seeds its model-tree from. Build keys with <see cref="SceneViewKey.Rel"/> /
  /// <see cref="SceneViewKey.Eav"/>.</summary>
  public void AddSceneView(SceneView view) => _envelopeWriter.AddSceneView(view);

  /// <summary>Authors one named camera viewpoint into <c>envelope.camera_views.parquet</c>. Position/target in
  /// model units, forward/up unit vectors, <see cref="CameraView.Fov"/> vertical DEGREES (perspective only).</summary>
  public void AddCameraView(CameraView view) => _envelopeWriter.AddCameraView(view);

  /// <summary>
  /// Records the producer information of this bundle in the <c>meta</c> file.
  /// </summary>
  /// <param name="producer">Producer info</param>
  /// <param name="migratedFromSchemaVersion">The original schema version for model versions migrated from older schema version. <see langword="null"/> for native authored versions</param>
  /// <remarks>Required before <see cref="Complete"/> — every bundle names its producer.</remarks>
  public void SetProducer(ISpeckleApplication producer, int? migratedFromSchemaVersion = null)
  {
    _envelopeWriter.SetProducer(
      producer.Slug,
      producer.HostApplicationVersion,
      "Speckle.Sdk (.NET)",
      SdkVersion,
      migratedFromSchemaVersion
    );
  }

  /// <summary>The version of this SDK, for <c>meta.sdk_version</c>: the package's informational version
  /// (<c>2026.9.0-alpha.7</c>) without the build-metadata suffix. Not <see cref="ISpeckleApplication.SpeckleVersion"/>,
  /// which a host registers as whatever it likes (a connector's own assembly version).</summary>
  internal static string SdkVersion { get; } = ReadSdkVersion();

  private static string ReadSdkVersion()
  {
    string? informational = typeof(ObjectsArtifactPipeline)
      .Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
      .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
      .FirstOrDefault()
      ?.InformationalVersion;
    if (informational is not { Length: > 0 })
    {
      return typeof(ObjectsArtifactPipeline).Assembly.GetName().Version?.ToString() ?? "unknown";
    }
    int plus = informational.IndexOf('+');
    return plus < 0 ? informational : informational.Substring(0, plus);
  }

  /// <summary>REMOVED — the <c>proxies(type, data JSON)</c> envelope is gone; use the typed
  /// node/relation API (<see cref="AddDefinition"/>, <see cref="AddMaterial"/>, <see cref="Display"/>, …).
  /// Kept (non-<c>[Obsolete]</c>, to avoid breaking the warnings-as-errors build of the parked Navis
  /// path) as a throwing stub; it fails loudly only if actually invoked.</summary>
  public void AddProxy(string type, string dataJson) =>
    throw new NotSupportedException(
      "AddProxy was removed with the proxies(type,json) envelope. Use the typed relations+nodes API."
    );

  /// <summary>Enqueues every artefact's final flush + finalize, then BLOCKS until the background writer has
  /// drained — so all parquet files are written and closed on return. Re-throws if any background write
  /// faulted.</summary>
  public void Complete()
  {
    _geometriesWriter.Complete();
    _envelopeWriter.Complete();
    _eavWriter.Complete();
    _structuralResultsWriter?.Complete(); // absent unless a producer added structural rows
    _modelEavWriter?.Complete(); // absent unless a producer added model-scoped rows
    _propertySetDefinitionsWriter?.Complete(); // absent unless a producer shipped set schemas
    _scheduler.CompleteAndWait();
  }

  // Cleanup path: finalize each writer (best-effort), then drain + join so file handles close.
  // Never let a writer's cleanup error escape (it fires during unwind).
  public void Dispose()
  {
    SafeDispose(_geometriesWriter);
    SafeDispose(_envelopeWriter);
    SafeDispose(_eavWriter);
    if (_structuralResultsWriter is not null)
    {
      SafeDispose(_structuralResultsWriter);
    }
    if (_modelEavWriter is not null)
    {
      SafeDispose(_modelEavWriter);
    }
    if (_propertySetDefinitionsWriter is not null)
    {
      SafeDispose(_propertySetDefinitionsWriter);
    }
    SafeDispose(_scheduler);
  }

  private static string FormatTransform(IReadOnlyList<double> transform) =>
    string.Join(",", transform.Select(d => d.ToString("R", CultureInfo.InvariantCulture)));

  private static void SafeDispose(IDisposable writer)
  {
    try
    {
      writer.Dispose();
    }
#pragma warning disable CA1031 // cleanup path: swallow so the original failure propagates unmasked
    catch (Exception)
#pragma warning restore CA1031
    {
      // Intentionally ignored.
    }
  }
}
#endif
