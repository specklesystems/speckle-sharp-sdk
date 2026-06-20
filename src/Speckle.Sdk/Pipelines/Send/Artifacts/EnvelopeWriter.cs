#if NET8_0_OR_GREATER
using System.Globalization;
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

  /// <summary>node(DEFINITION) → geometry | node(nested INSTANCE). Definition membership.</summary>
  public const byte Defines = 4;

  /// <summary>geometry → node(MATERIAL). Per-mesh render material.</summary>
  public const byte HasMaterial = 5;

  /// <summary>geometry | object → node(COLOR). Display colour.</summary>
  public const byte HasColor = 6;

  /// <summary>object → node(LEVEL). Level membership.</summary>
  public const byte OnLevel = 7;

  /// <summary>object → node(INSTANCE). Renderable via a placement (transform + definition).</summary>
  public const byte DisplayInstance = 8;
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
///   {base}.envelope.manifest.sql                               -- attaches the above as views
/// </code>
/// <c>transform</c> is 16 row-major doubles, comma-separated. Not thread-safe: calls are sequential.
/// </summary>
public sealed class EnvelopeWriter : IDisposable
{
  private const int SCHEMA_VERSION = 1;

  public string OutputDir { get; }
  public string BaseName { get; }

  /// <summary>The manifest entry point (kept named <c>EnvelopeDbPath</c> for caller compatibility).</summary>
  public string EnvelopeDbPath { get; }

  private readonly ParquetTableWriter _relations;
  private readonly ParquetTableWriter _nodes;
  private bool _completed;

  public EnvelopeWriter(string outputDir, string baseName)
  {
    Directory.CreateDirectory(outputDir);
    OutputDir = outputDir;
    BaseName = baseName;
    EnvelopeDbPath = P("manifest.sql");

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
    File.WriteAllText(EnvelopeDbPath, Manifest());
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
    }
    using var nk = new ParquetTableWriter(P("node_kinds.parquet"), new ParquetSchema(I("kind"), S("name")));
    nk.AddRow(1, "DEFINITION");
    nk.AddRow(2, "INSTANCE");
    nk.AddRow(3, "MATERIAL");
    nk.AddRow(4, "COLOR");
    nk.AddRow(5, "LEVEL");
  }

  private string P(string suffix) => Path.Combine(OutputDir, $"{BaseName}.envelope.{suffix}");

  private string Manifest() =>
    string.Create(
      CultureInfo.InvariantCulture,
      $@"-- Speckle 4.0 envelope artefact. Run from the artefact directory (DuckDB reads parquet natively).
CREATE VIEW relations  AS SELECT * FROM read_parquet('{BaseName}.envelope.relations.parquet');
CREATE VIEW nodes      AS SELECT * FROM read_parquet('{BaseName}.envelope.nodes.parquet');
CREATE VIEW meta       AS SELECT * FROM read_parquet('{BaseName}.envelope.meta.parquet');
CREATE VIEW rel_types  AS SELECT * FROM read_parquet('{BaseName}.envelope.rel_types.parquet');
CREATE VIEW node_kinds AS SELECT * FROM read_parquet('{BaseName}.envelope.node_kinds.parquet');
"
    );

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
