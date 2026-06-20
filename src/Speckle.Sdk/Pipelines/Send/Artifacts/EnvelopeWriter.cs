#if NET8_0_OR_GREATER
using Parquet.Schema;

namespace Speckle.Sdk.Pipelines.Send.Artifacts;

/// <summary>Edge kinds in the envelope <c>relations</c> table. The <c>rel</c> fixes the identity
/// namespace of <c>src</c>/<c>dst</c> (see <c>notes/topology-envelope-SOT.md</c> §2).</summary>
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
}

/// <summary>Value-node kinds in the envelope <c>nodes</c> table.</summary>
public static class NodeKind
{
  public const byte Definition = 1;
  public const byte Instance = 2;
  public const byte Material = 3;
  public const byte Color = 4;
  public const byte Level = 5;
}

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
/// </code>
/// No manifest is written — consumers build their own <c>read_parquet</c> views (SOT §4).
/// <c>transform</c> is 16 row-major doubles, comma-separated. Not thread-safe: calls are sequential.
/// </summary>
public sealed class EnvelopeWriter : IDisposable
{
  private const int SCHEMA_VERSION = 1;

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
  private bool _completed;

  public EnvelopeWriter(string outputDir, string baseName)
  {
    Directory.CreateDirectory(outputDir);
    OutputDir = outputDir;
    BaseName = baseName;

    _relations = new ParquetTableWriter(
      P("relations.parquet"),
      new ParquetSchema(I("rel"), I("src"), I("dst"), I("ord"))
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
      )
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
    using (var meta = new ParquetTableWriter(P("meta.parquet"), new ParquetSchema(I("schema_version"), S("produced_by"))))
    {
      meta.AddRow(SCHEMA_VERSION, "Speckle.Sdk EnvelopeWriter");
    }
    using (
      var rt = new ParquetTableWriter(
        P("rel_types.parquet"),
        new ParquetSchema(I("rel"), S("name"), S("src_ns"), S("dst_ns"))
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
    }
    using var nk = new ParquetTableWriter(P("node_kinds.parquet"), new ParquetSchema(I("kind"), S("name")));
    nk.AddRow(1, "DEFINITION");
    nk.AddRow(2, "INSTANCE");
    nk.AddRow(3, "MATERIAL");
    nk.AddRow(4, "COLOR");
    nk.AddRow(5, "LEVEL");
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
