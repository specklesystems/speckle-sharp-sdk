#if NET8_0_OR_GREATER
using System.Globalization;
using Parquet.Schema;

namespace Speckle.Sdk.Pipelines.Send.Artifacts;

/// <summary>Edge kinds in the envelope <c>relations</c> table. The <c>rel</c> fixes the identity
/// namespace of <c>src</c>/<c>dst</c> (see <c>notes/topology-envelope-SOT.md</c> §2).
///
/// CROSS-CONNECTOR NAMING CONVENTION (ENG-8690 residual): the same topological concept MUST get the
/// same rel code + <c>rel_types.name</c> across every producer (Revit / IFC / Civil3D / Navis / …).
/// This enum plus the <c>rel_types</c> rows in <see cref="EnvelopeWriter.WriteCatalog"/> are the SINGLE
/// source of that vocabulary — connectors emit through the typed pipeline methods, never an ad-hoc name.
/// The catalog is self-describing, so new rels are additive and non-breaking (consumers skip unknown
/// rels); add each one in the same change as the connector that first emits it, and bump
/// <c>schema_version</c>.</summary>
public static class RelKind
{
  /// <summary>object → geometry. Direct renderable geometry (world-coord mesh). <c>ord</c> = fragment.</summary>
  public const byte Display = 1;

  /// <summary>object → geometry. Authoritative solid (Brep/Solid).</summary>
  public const byte Solid = 2;

  /// <summary>object → object. Host→hosted nesting (curtain wall → panels).</summary>
  public const byte Subelement = 3;

  /// <summary>node(DEFINITION) → geometry. Definition contains a raw mesh member.</summary>
  public const byte Defines = 4;

  /// <summary>geometry → node(MATERIAL). Per-mesh render material.</summary>
  public const byte HasMaterial = 5;

  /// <summary>geometry | object → node(COLOR). Display colour.</summary>
  public const byte HasColor = 6;

  /// <summary>object → node(LEVEL). Level membership.</summary>
  public const byte OnLevel = 7;

  /// <summary>object → node(INSTANCE). Renderable via a placement (transform + definition).</summary>
  public const byte DisplayInstance = 8;

  /// <summary>node(DEFINITION) → node(nested INSTANCE). Definition contains a nested block placement.
  /// Split from <see cref="Defines"/> so <c>rel</c> fixes the dst namespace (geometry vs node) — the same
  /// reason <see cref="Display"/>/<see cref="DisplayInstance"/> are split; per-namespace ids overlap.</summary>
  public const byte DefinesInstance = 9;

  /// <summary>object → node(COLLECTION). The object's direct membership in a scene-tree container (Rhino/CAD
  /// layer, ETABS story/category, Tekla category, …). The container TREE is the COLLECTION node's
  /// <c>def_ref</c> = parent collection chain. Lets the loader rebuild the source hierarchy (otherwise lost
  /// when the scene is flattened). Source root collection excluded.</summary>
  public const byte InCollection = 10;

  /// <summary>object → node(CONTAINER, subtype "Model"). The object's source-document / host / linked-model
  /// membership (Revit linked models, Navis source document, DGN models &amp; reference attachments). A
  /// DEDICATED rel — NOT a <see cref="InCollection"/> subtype — so it can sit explicitly atop a default
  /// scene_views projection (e.g. Revit IN_MODEL → ON_LEVEL → category). Self-nesting via the CONTAINER
  /// node's <c>def_ref</c> (parent model) for nested links / appended-within-appended. SOT §8.</summary>
  public const byte InModel = 11;
}

/// <summary>Value-node kinds in the envelope <c>nodes</c> table.</summary>
public static class NodeKind
{
  public const byte Definition = 1;
  public const byte Instance = 2;
  public const byte Material = 3;
  public const byte Color = 4;
  public const byte Level = 5;

  /// <summary>A scene-tree container (layer / category / story). <c>name</c> = its name, <c>def_ref</c> = its
  /// PARENT collection node (null = top-level, directly under the excluded root), <c>units</c> = its subtype
  /// tag (e.g. "Layer", "Collection", or the source <c>collectionType</c>) so the loader can label it.</summary>
  public const byte Collection = 6;

  /// <summary>A generic SEMANTIC-TOPOLOGY container (model / room / space / system / …), distinct from the
  /// authored <see cref="Collection"/> scene-tree. The inbound rel (<see cref="RelKind.InModel"/>, …)
  /// disambiguates which axis it is; <c>name</c> = its label, <c>def_ref</c> = its PARENT container
  /// (null = top-level, self-nesting), <c>units</c> = its subtype tag (e.g. "Model"). One generic kind keeps
  /// node_kinds stable as the rel vocabulary grows (SOT §8).
  ///
  /// WHY DISTINCT FROM <see cref="Collection"/> (they share columns): a COLLECTION is a SINGLE membership —
  /// an object lives at exactly one spot in one authored tree (its leaf layer), reached only by
  /// <see cref="RelKind.InCollection"/>. CONTAINER axes are INDEPENDENT and SIMULTANEOUS — the same object
  /// can be "from linked model Structural.rvt" AND "in Room 101" AND "on system HVAC-2" at once, each via a
  /// different rel. They can't share one membership relation: if model and layer were both IN_COLLECTION into
  /// the same kind you could not express "group by model, THEN by the layer tree" — the two axes would
  /// collapse into one. Separate kind + dedicated rels keep each a reorderable scene_views projection key.</summary>
  public const byte Container = 7;
}

/// <summary>The store a <see cref="SceneViewKey"/> reads from: a relation walk or an eav group-by.</summary>
public enum ProjectionSource
{
  /// <summary>Relation walk — <see cref="SceneViewKey.Ref"/> is a <see cref="RelKind"/> code (joins rel_types).</summary>
  Rel,

  /// <summary>EAV group-by — <see cref="SceneViewKey.Ref"/> is the eav attribute key.</summary>
  Eav,
}

/// <summary>One ordered key of a <see cref="SceneView"/> projection. Build via <see cref="Rel"/> / <see cref="Eav"/>
/// so <see cref="Ref"/> is encoded correctly (a rel code as int-string, or a bare eav attr key).</summary>
public sealed record SceneViewKey(ProjectionSource Source, string Ref)
{
  /// <summary>A relation-walk key, e.g. <c>SceneViewKey.Rel(RelKind.OnLevel)</c>.</summary>
  public static SceneViewKey Rel(byte rel) =>
    new(ProjectionSource.Rel, ((int)rel).ToString(CultureInfo.InvariantCulture));

  /// <summary>An eav group-by key, e.g. <c>SceneViewKey.Eav("category")</c>.</summary>
  public static SceneViewKey Eav(string attrKey) => new(ProjectionSource.Eav, attrKey);
}

/// <summary>A producer-authored scene-explorer projection (SOT §8): an ordered list of grouping keys the
/// consumer seeds the model-tree grouping from. Exactly one view per artefact should be
/// <see cref="IsDefault"/>; <see cref="Keys"/> are outermost-first. Producers OMIT keys with no data
/// (conditional keys, e.g. IN_MODEL for a single-model file) and consumers also skip single/empty-group
/// keys, so default ≠ only — users can re-pivot or pick a named alternate.</summary>
public sealed record SceneView(int View, string Name, bool IsDefault, IReadOnlyList<SceneViewKey> Keys);

/// <summary>
/// Writes the Speckle 4.0 envelope topology artefact — now DIRECT Zstd PARQUET (one file per table)
/// instead of DuckDB, à la <see cref="GeometriesParquetWriter"/>: passive columnar files, no
/// WAL/checkpoint/index, ~90% smaller than the .duckdb form. Pure dense <c>int32</c> identity.
/// <code>
///   {base}.envelope.relations.parquet(rel, src, dst, ord)      -- typed edges; src/dst namespace = rel
///   {base}.envelope.nodes.parquet(id, kind, name, def_ref,     -- shared value-entities (definition /
///         transform, units, argb, opacity, metalness,             instance / material / colour / level)
///         roughness, elevation)
///   {base}.envelope.{meta,rel_types,node_kinds}.parquet        -- self-describing catalog (SOT §6)
///   {base}.envelope.scene_views.parquet(view, name,           -- producer-authored default + named
///         is_default, ord, source, ref)                          grouping projections (SOT §8); absent if none
/// </code>
/// No manifest is written — consumers build their own <c>read_parquet</c> views (SOT §4).
/// <c>transform</c> is 16 row-major doubles, comma-separated. Not thread-safe: calls are sequential.
/// </summary>
public sealed class EnvelopeWriter : IDisposable
{
  // 2: + IN_MODEL rel & CONTAINER node kind (SOT §8). Additive — consumers skip unknown rels/kinds.
  private const int SCHEMA_VERSION = 2;

  public string OutputDir { get; }
  public string BaseName { get; }

  /// <summary>
  /// The directory holding this artefact's parquet tables. No single entry-point file — consumers build their
  /// own <c>read_parquet</c> views (see <c>notes/topology-envelope-SOT.md</c> §4). Kept named
  /// <c>EnvelopeDbPath</c> for caller compatibility.
  /// </summary>
  public string EnvelopeDbPath => OutputDir;

  private readonly ParquetTableWriter _relations;
  private readonly ParquetTableWriter _nodes;
  private readonly ParquetWriteScheduler _scheduler;
  private readonly List<SceneView> _sceneViews = new();
  private bool _completed;

  public EnvelopeWriter(string outputDir, string baseName, ParquetWriteScheduler scheduler)
  {
    Directory.CreateDirectory(outputDir);
    OutputDir = outputDir;
    BaseName = baseName;
    _scheduler = scheduler;

    _relations = new ParquetTableWriter(
      P("relations.parquet"),
      new ParquetSchema(I("rel"), I("src"), I("dst"), I("ord")),
      scheduler
    );
    _nodes = new ParquetTableWriter(
      P("nodes.parquet"),
      new ParquetSchema(
        I("id"),
        I("kind"),
        S("name"),
        new DataField<int?>("def_ref"),
        S("transform"),
        S("units"),
        new DataField<int?>("argb"),
        new DataField<double?>("opacity"),
        new DataField<double?>("metalness"),
        new DataField<double?>("roughness"),
        new DataField<double?>("elevation")
      ),
      scheduler
    );

    WriteCatalog();
  }

  /// <summary>Appends one typed edge. <paramref name="src"/>/<paramref name="dst"/> are dense ids in
  /// the namespaces fixed by <paramref name="rel"/> (see <see cref="RelKind"/>).</summary>
  public void AddRelation(byte rel, int src, int dst, int ord)
  {
    EnsureNotCompleted();
    _relations.AddRow((int)rel, src, dst, ord);
  }

  /// <summary>Appends one value-node. Only the columns relevant to <paramref name="kind"/> are
  /// non-null (see <see cref="NodeKind"/>).</summary>
  public void AddNode(
    int id,
    byte kind,
    string? name,
    int? defRef,
    string? transform,
    string? units,
    int? argb,
    double? opacity,
    double? metalness,
    double? roughness,
    double? elevation
  )
  {
    EnsureNotCompleted();
    _nodes.AddRow(id, (int)kind, name, defRef, transform, units, argb, opacity, metalness, roughness, elevation);
  }

  /// <summary>Buffers a producer-authored projection (SOT §8); flushed to <c>scene_views.parquet</c> on
  /// <see cref="Complete"/>. Add none and the table is simply absent (consumer falls back to its own default).</summary>
  public void AddSceneView(SceneView view)
  {
    EnsureNotCompleted();
    _sceneViews.Add(view);
  }

  /// <summary>Flushes the parquet tables and writes the attach manifest.</summary>
  public void Complete()
  {
    if (_completed)
    {
      return;
    }
    _completed = true;
    _relations.Complete();
    _nodes.Complete();
    WriteSceneViews();
  }

  public void Dispose()
  {
    if (_completed)
    {
      return;
    }
    _completed = true;
    SafeDispose(_relations);
    SafeDispose(_nodes);
  }

  // Self-describing catalog (SOT §6): the rel/kind vocabulary + schema version, written once. Tiny.
  private void WriteCatalog()
  {
    using (
      var meta = new ParquetTableWriter(
        P("meta.parquet"),
        new ParquetSchema(I("schema_version"), S("produced_by")),
        _scheduler
      )
    )
    {
      meta.AddRow(SCHEMA_VERSION, "Speckle.Sdk EnvelopeWriter");
    }
    using (
      var rt = new ParquetTableWriter(
        P("rel_types.parquet"),
        new ParquetSchema(I("rel"), S("name"), S("src_ns"), S("dst_ns")),
        _scheduler
      )
    )
    {
      rt.AddRow(1, "DISPLAY", "object", "geometry");
      rt.AddRow(2, "SOLID", "object", "geometry");
      rt.AddRow(3, "SUBELEMENT", "object", "object");
      rt.AddRow(4, "DEFINES", "node", "geometry");
      rt.AddRow(5, "HAS_MATERIAL", "geometry", "node");
      rt.AddRow(6, "HAS_COLOR", "geometry|object", "node");
      rt.AddRow(7, "ON_LEVEL", "object", "node");
      rt.AddRow(8, "DISPLAY_INSTANCE", "object", "node");
      rt.AddRow(9, "DEFINES_INSTANCE", "node", "node");
      rt.AddRow(10, "IN_COLLECTION", "object", "node");
      rt.AddRow(11, "IN_MODEL", "object", "node");
    }
    using var nk = new ParquetTableWriter(
      P("node_kinds.parquet"),
      new ParquetSchema(I("kind"), S("name")),
      _scheduler
    );
    nk.AddRow(1, "DEFINITION");
    nk.AddRow(2, "INSTANCE");
    nk.AddRow(3, "MATERIAL");
    nk.AddRow(4, "COLOR");
    nk.AddRow(5, "LEVEL");
    nk.AddRow(6, "COLLECTION");
    nk.AddRow(7, "CONTAINER");
  }

  // Producer-authored projections (SOT §8) — written at Complete(), not eagerly like the static catalog,
  // since the producer declares views over the artefact's lifetime. Absent (no file) when none authored.
  private void WriteSceneViews()
  {
    if (_sceneViews.Count == 0)
    {
      return;
    }
    using var sv = new ParquetTableWriter(
      P("scene_views.parquet"),
      new ParquetSchema(I("view"), S("name"), B("is_default"), I("ord"), S("source"), S("ref")),
      _scheduler
    );
    foreach (var v in _sceneViews)
    {
      for (var ord = 0; ord < v.Keys.Count; ord++)
      {
        var key = v.Keys[ord];
        sv.AddRow(v.View, v.Name, v.IsDefault, ord, key.Source == ProjectionSource.Rel ? "rel" : "eav", key.Ref);
      }
    }
  }

  private string P(string suffix) => Path.Combine(OutputDir, $"{BaseName}.envelope.{suffix}");

  private void EnsureNotCompleted()
  {
    if (_completed)
    {
      throw new InvalidOperationException("Writer already completed.");
    }
  }

  private static DataField I(string name) => new DataField<int>(name);

  private static DataField S(string name) => new DataField<string>(name);

  private static DataField B(string name) => new DataField<bool>(name);

  private static void SafeDispose(IDisposable d)
  {
    try
    {
      d.Dispose();
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
