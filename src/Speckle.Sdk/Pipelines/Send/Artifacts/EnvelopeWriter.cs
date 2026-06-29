#if NETSTANDARD2_0 || NET8_0_OR_GREATER
using System.Globalization;
using Parquet.Schema;
using SpecRel = Speckle.Bundle.Spec.Rel;
using SpecKind = Speckle.Bundle.Spec.NodeKind;
using SpecCatalog = Speckle.Bundle.Spec.Catalog;
using SpecBundle = Speckle.Bundle.Spec.BundleSpec;
using SpecSchemas = Speckle.Bundle.Spec.BundleSchemas;
using SpecColumn = Speckle.Bundle.Spec.ColumnSpec;
using SpecArrow = Speckle.Bundle.Spec.ArrowType;

namespace Speckle.Sdk.Pipelines.Send.Artifacts;

/// <summary>Edge kinds in the envelope <c>relations</c> table — a thin PascalCase facade over the
/// generated <c>Speckle.Bundle.Spec.Rel</c> enum so connectors keep an idiomatic C# vocabulary while the
/// VALUES, the <c>rel_types</c> catalog, and the table schemas all come from the single source of truth
/// (the <c>speckle-bundle-spec</c> sibling repo). Add a new rel in the spec and regenerate; retired rels
/// are simply absent here. See <see cref="EnvelopeWriter.WriteCatalog"/>.</summary>
public static class RelKind
{
  public const byte Display = (byte)SpecRel.DISPLAY;
  public const byte Solid = (byte)SpecRel.SOLID;
  public const byte Subelement = (byte)SpecRel.SUBELEMENT;
  public const byte Defines = (byte)SpecRel.DEFINES;
  public const byte HasMaterial = (byte)SpecRel.HAS_MATERIAL;
  public const byte HasColor = (byte)SpecRel.HAS_COLOR;
  public const byte OnLevel = (byte)SpecRel.ON_LEVEL;
  public const byte DisplayInstance = (byte)SpecRel.DISPLAY_INSTANCE;
  public const byte DefinesInstance = (byte)SpecRel.DEFINES_INSTANCE;
  public const byte InCollection = (byte)SpecRel.IN_COLLECTION;
  public const byte InModel = (byte)SpecRel.IN_MODEL;
  public const byte InRoom = (byte)SpecRel.IN_ROOM;
  public const byte InSystem = (byte)SpecRel.IN_SYSTEM;
  public const byte ConnectsTo = (byte)SpecRel.CONNECTS_TO;
  public const byte Bounds = (byte)SpecRel.BOUNDS;
}

/// <summary>Value-node kinds in the envelope <c>nodes</c> table — a thin facade over the generated
/// <c>Speckle.Bundle.Spec.NodeKind</c> enum. COLLECTION folded into CONTAINER (v5): a scene-tree
/// collection is a CONTAINER whose <c>subtype</c> carries its tag; the inbound rel (<c>IN_COLLECTION</c>
/// vs <c>IN_MODEL</c>/<c>IN_SYSTEM</c>/…) disambiguates the grouping axis.</summary>
public static class NodeKind
{
  public const byte Definition = (byte)SpecKind.DEFINITION;
  public const byte Instance = (byte)SpecKind.INSTANCE;
  public const byte Material = (byte)SpecKind.MATERIAL;
  public const byte Color = (byte)SpecKind.COLOR;
  public const byte Level = (byte)SpecKind.LEVEL;
  public const byte Container = (byte)SpecKind.CONTAINER;
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
/// Writes the Speckle 4.0 envelope topology artefact as DIRECT Zstd PARQUET (one file per table). The
/// table SHAPES and the self-describing catalog (<c>rel_types</c>/<c>node_kinds</c>/<c>meta</c>) come from
/// the generated <c>speckle-bundle-spec</c> — this writer no longer hand-declares them.
/// <code>
///   {base}.envelope.relations.parquet(rel, src, dst, ord)      -- typed edges; src/dst namespace = rel
///   {base}.envelope.nodes.parquet(id, kind, name, def_ref,     -- shared value-entities; `subtype` is the
///         transform, units, subtype, argb, opacity,                CONTAINER discriminator (Model/Collection/…)
///         metalness, roughness, elevation)
///   {base}.envelope.{meta,rel_types,node_kinds}.parquet        -- self-describing catalog (SOT §6)
///   {base}.envelope.scene_views.parquet(view, name,           -- producer-authored grouping projections
///         is_default, ord, source, ref)                          (SOT §8); absent if none
/// </code>
/// <c>transform</c> is 16 row-major doubles, comma-separated. Not thread-safe: calls are sequential.
/// </summary>
public sealed class EnvelopeWriter : IDisposable
{
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

    _relations = new ParquetTableWriter(P("relations.parquet"), SchemaOf(SpecSchemas.Relations), scheduler);
    _nodes = new ParquetTableWriter(P("nodes.parquet"), SchemaOf(SpecSchemas.Nodes), scheduler);

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
  /// non-null (see <see cref="NodeKind"/>). <paramref name="subtype"/> is the CONTAINER discriminator
  /// (e.g. "Model", "Network") — null for non-container kinds.</summary>
  public void AddNode(
    int id,
    byte kind,
    string? name,
    int? defRef,
    string? transform,
    string? units,
    string? subtype,
    int? argb,
    double? opacity,
    double? metalness,
    double? roughness,
    double? elevation
  )
  {
    EnsureNotCompleted();
    _nodes.AddRow(id, (int)kind, name, defRef, transform, units, subtype, argb, opacity, metalness, roughness, elevation);
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

  // Self-describing catalog (SOT §6): the rel/kind vocabulary + schema version, written once from the
  // generated spec catalog (live + reserved rows; retired ids are absent and never reused). Tiny.
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
      meta.AddRow(SpecBundle.SchemaVersion, "Speckle.Sdk EnvelopeWriter");
    }
    using (
      var rt = new ParquetTableWriter(
        P("rel_types.parquet"),
        new ParquetSchema(I("rel"), S("name"), S("src_ns"), S("dst_ns")),
        _scheduler
      )
    )
    {
      foreach (var r in SpecCatalog.RelTypes)
      {
        rt.AddRow(r.Id, r.Name, r.SrcNs, r.DstNs);
      }
    }
    using var nk = new ParquetTableWriter(
      P("node_kinds.parquet"),
      new ParquetSchema(I("kind"), S("name")),
      _scheduler
    );
    foreach (var k in SpecCatalog.NodeKinds)
    {
      nk.AddRow(k.Id, k.Name);
    }
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

  // Build a Parquet.Net schema from the generated spec column descriptors (the single source of truth
  // for table shapes). DDL nullability → DataField<T> (required) vs DataField<T?> (nullable).
  private static ParquetSchema SchemaOf(SpecColumn[] cols)
  {
    var fields = new Field[cols.Length];
    for (var i = 0; i < cols.Length; i++)
    {
      fields[i] = ToField(cols[i]);
    }
    return new ParquetSchema(fields);
  }

  private static DataField ToField(SpecColumn c) =>
    c.Type switch
    {
      SpecArrow.Int32 => c.Nullable ? new DataField<int?>(c.Name) : new DataField<int>(c.Name),
      SpecArrow.Int64 => c.Nullable ? new DataField<long?>(c.Name) : new DataField<long>(c.Name),
      SpecArrow.Float64 => c.Nullable ? new DataField<double?>(c.Name) : new DataField<double>(c.Name),
      SpecArrow.Boolean => c.Nullable ? new DataField<bool?>(c.Name) : new DataField<bool>(c.Name),
      SpecArrow.Utf8 => new DataField<string>(c.Name),
      SpecArrow.Binary => new DataField<byte[]>(c.Name),
      _ => throw new NotSupportedException($"Unmapped ArrowType {c.Type} for column {c.Name}"),
    };

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
