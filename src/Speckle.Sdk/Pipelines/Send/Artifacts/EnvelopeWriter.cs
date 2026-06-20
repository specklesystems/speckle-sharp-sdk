#if NET8_0_OR_GREATER
using System.Globalization;
using DuckDB.NET.Data;

namespace Speckle.Sdk.Pipelines.Send.Artifacts;

/// <summary>Edge kinds in <see cref="EnvelopeWriter"/>'s <c>relations</c> table.
/// The <c>rel</c> fixes the identity namespace of <c>src</c>/<c>dst</c> (see
/// <c>notes/topology-envelope-SOT.md</c> §2): object / geometry / node.</summary>
public static class RelKind
{
  /// <summary>object → geometry. Direct renderable geometry (world-coord mesh). <c>ord</c> = fragment.</summary>
  public const byte Display = 1;

  /// <summary>object → node(INSTANCE). Renderable via a placement (transform + definition). <c>ord</c> = fragment.
  /// Distinct from <see cref="Display"/> so <c>dst</c>'s namespace (geometry vs node) is unambiguous —
  /// node and geometry ids are separate per-namespace counters and would otherwise collide.</summary>
  public const byte DisplayInstance = 8;

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
}

/// <summary>Value-node kinds in <see cref="EnvelopeWriter"/>'s <c>nodes</c> table.</summary>
public static class NodeKind
{
  public const byte Definition = 1;
  public const byte Instance = 2;
  public const byte Material = 3;
  public const byte Color = 4;
  public const byte Level = 5;
}

/// <summary>
/// Writes the Speckle 4.0 <c>envelope.duckdb</c> artefact client-side — the topology
/// property graph from <c>notes/topology-envelope-SOT.md</c>. Two tables, pure dense
/// <c>int32</c> identity (no <c>applicationId</c> strings):
/// <code>
///   relations(rel, src, dst, ord)   -- typed edges; src/dst namespace is rel-determined
///   nodes(id, kind, name, def_ref,  -- shared value-entities (definition / instance /
///         transform, units, argb,      material / colour / level), sparse per kind
///         opacity, metalness, roughness, elevation)
/// </code>
/// Supersedes the old <c>proxies(type, data JSON)</c> shape. Material/colour/level are
/// value-nodes here (not eav attributes): they bind to objects OR meshes, and meshes are
/// not eav entities. Geometry blobs live in <see cref="GeometriesParquetWriter"/>;
/// per-object labels + the identity dictionary live in <see cref="EavWriter"/>.
///
/// All <c>src</c>/<c>dst</c>/<c>id</c> values are dense ints minted by the caller's
/// per-namespace <see cref="IdInterner"/>s. <c>transform</c> is 16 row-major doubles,
/// comma-separated (invariant culture). Not thread-safe: calls are sequential.
/// </summary>
public sealed class EnvelopeWriter : IDisposable
{
  // Resource governance only — does not affect produced content.
  private const string MEMORY_LIMIT_MB_ENV_VAR = "SPECKLE_DUCKDB_MEMORY_LIMIT_MB";
  private const int DEFAULT_MEMORY_LIMIT_MB = 256;
  private const int APPENDER_RECYCLE_INTERVAL = 25_000;

  /// <summary>Bumped when the relations/nodes/catalog schema changes (written to <c>meta</c>).</summary>
  private const int SCHEMA_VERSION = 1;

  public string EnvelopeDbPath { get; }

  private readonly DuckDBConnection _db;
  private DuckDBAppender _relationsAppender;
  private DuckDBAppender _nodesAppender;
  private int _relRowsSinceFlush;
  private int _nodeRowsSinceFlush;
  private bool _completed;

  public EnvelopeWriter(string outputDir, string baseName)
  {
    Directory.CreateDirectory(outputDir);
    EnvelopeDbPath = Path.Combine(outputDir, $"{baseName}.envelope.duckdb");

    DeleteIfExists(EnvelopeDbPath);

    var memoryLimitMb = ResolveMbEnvVar(MEMORY_LIMIT_MB_ENV_VAR, DEFAULT_MEMORY_LIMIT_MB);
    _db = new DuckDBConnection($"Data Source={EnvelopeDbPath}");
    _db.Open();
    Execute(
      _db,
      FormattableString.Invariant($"SET memory_limit='{memoryLimitMb}MB'"),
      "SET threads=4",
      "CREATE TABLE relations (rel INTEGER NOT NULL, src INTEGER NOT NULL, dst INTEGER NOT NULL, ord INTEGER NOT NULL)",
      @"CREATE TABLE nodes (
        id INTEGER NOT NULL,
        kind INTEGER NOT NULL,
        name VARCHAR,
        def_ref INTEGER,
        transform VARCHAR,
        units VARCHAR,
        argb INTEGER,
        opacity DOUBLE,
        metalness DOUBLE,
        roughness DOUBLE,
        elevation DOUBLE
      )",
      // Self-describing catalog (notes/topology-envelope-SOT.md §6): the rel/kind vocabulary travels
      // IN the artefact so a generic consumer learns meaning + namespaces without our source, and can
      // skip unknown rels/kinds gracefully. rel/kind stay compact ints in the hot tables.
      $"CREATE TABLE meta (schema_version INTEGER NOT NULL, produced_by VARCHAR)",
      $"INSERT INTO meta VALUES ({SCHEMA_VERSION}, 'Speckle.Sdk EnvelopeWriter')",
      "CREATE TABLE rel_types (rel INTEGER PRIMARY KEY, name VARCHAR, src_ns VARCHAR, dst_ns VARCHAR)",
      @"INSERT INTO rel_types VALUES
        (1,'DISPLAY','object','geometry'),
        (2,'SOLID','object','geometry'),
        (3,'SUBELEMENT','object','object'),
        (4,'DEFINES','node','geometry'),
        (5,'HAS_MATERIAL','geometry','node'),
        (6,'HAS_COLOR','geometry|object','node'),
        (7,'ON_LEVEL','object','node'),
        (8,'DISPLAY_INSTANCE','object','node')",
      "CREATE TABLE node_kinds (kind INTEGER PRIMARY KEY, name VARCHAR)",
      @"INSERT INTO node_kinds VALUES
        (1,'DEFINITION'),(2,'INSTANCE'),(3,'MATERIAL'),(4,'COLOR'),(5,'LEVEL')"
    );
    _relationsAppender = _db.CreateAppender("relations");
    _nodesAppender = _db.CreateAppender("nodes");
  }

  /// <summary>Appends one typed edge. <paramref name="src"/>/<paramref name="dst"/> are
  /// dense ids in the namespaces fixed by <paramref name="rel"/> (see <see cref="RelKind"/>).</summary>
  public void AddRelation(byte rel, int src, int dst, int ord)
  {
    EnsureNotCompleted();
    _relationsAppender.CreateRow().AppendValue((int)rel).AppendValue(src).AppendValue(dst).AppendValue(ord).EndRow();
    if (++_relRowsSinceFlush >= APPENDER_RECYCLE_INTERVAL)
    {
      _relationsAppender.Dispose();
      _relationsAppender = _db.CreateAppender("relations");
      _relRowsSinceFlush = 0;
    }
  }

  /// <summary>Appends one value-node. Only the columns relevant to
  /// <paramref name="kind"/> are non-null (see <see cref="NodeKind"/>).</summary>
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
    _nodesAppender
      .CreateRow()
      .AppendValue(id)
      .AppendValue((int)kind)
      .AppendValue(name)
      .AppendValue(defRef)
      .AppendValue(transform)
      .AppendValue(units)
      .AppendValue(argb)
      .AppendValue(opacity)
      .AppendValue(metalness)
      .AppendValue(roughness)
      .AppendValue(elevation)
      .EndRow();
    if (++_nodeRowsSinceFlush >= APPENDER_RECYCLE_INTERVAL)
    {
      _nodesAppender.Dispose();
      _nodesAppender = _db.CreateAppender("nodes");
      _nodeRowsSinceFlush = 0;
    }
  }

  /// <summary>
  /// Flushes the appenders, checkpoints, and closes the connection so the file can be
  /// uploaded immediately (releases the DuckDB lock).
  /// </summary>
  public void Complete()
  {
    if (_completed)
    {
      return;
    }
    _completed = true;

    // Disposing the appenders flushes the final batch; disposing the connection
    // checkpoints the WAL into the file. No explicit CHECKPOINT — let close own the
    // single checkpoint (an explicit one can trip a DuckDB internal assertion).
    _relationsAppender.Dispose();
    _nodesAppender.Dispose();
    _db.Dispose();
  }

  public void Dispose() => Complete();

  private void EnsureNotCompleted()
  {
    if (_completed)
    {
      throw new InvalidOperationException("Writer already completed.");
    }
  }

  [System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "Statements are compile-time constants (DDL/settings); the only interpolated value is a parsed int."
  )]
  private static void Execute(DuckDBConnection conn, params string[] statements)
  {
    foreach (var sql in statements)
    {
      using var cmd = conn.CreateCommand();
      cmd.CommandText = sql;
      cmd.ExecuteNonQuery();
    }
  }

  private static int ResolveMbEnvVar(string name, int fallback)
  {
    var raw = Environment.GetEnvironmentVariable(name);
    return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mb) && mb > 0 ? mb : fallback;
  }

  private static void DeleteIfExists(string path)
  {
    if (File.Exists(path))
    {
      File.Delete(path);
    }
    if (File.Exists(path + ".wal"))
    {
      File.Delete(path + ".wal");
    }
  }
}
#endif
