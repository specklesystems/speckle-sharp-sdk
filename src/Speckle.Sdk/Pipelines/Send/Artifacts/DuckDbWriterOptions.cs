#if NET8_0_OR_GREATER
using System.Globalization;

namespace Speckle.Sdk.Pipelines.Send.Artifacts;

/// <summary>
/// Resource-governance knobs for <see cref="DuckDbArtifactWriter"/>. None of
/// them affect the produced content (same rows, same EAV, same indexes and
/// PKs; insertion order preserved) — they only shape WHEN work happens, so
/// memory behavior can be tuned and A/B-tested per run.
/// <para>
/// Every knob maps to a <c>SPECKLE_DUCKDB_*</c> env var (see each property);
/// <see cref="FromEnvironment"/> is what the writer uses when no options are
/// passed, so a consumer process (e.g. the oda importers) selects a variant
/// purely through its environment — no recompile. The writer echoes the
/// resolved values into the MemoryLog stream at startup, making every
/// profiled run self-describing.
/// </para>
/// </summary>
public sealed record DuckDbWriterOptions
{
  private const string MEMORY_LIMIT_MB_ENV_VAR = "SPECKLE_DUCKDB_MEMORY_LIMIT_MB";
  private const string INDEX_MEMORY_LIMIT_MB_ENV_VAR = "SPECKLE_DUCKDB_INDEX_MEMORY_LIMIT_MB";
  private const string RECYCLE_OBJECTS_ENV_VAR = "SPECKLE_DUCKDB_RECYCLE_OBJECTS";
  private const string RECYCLE_MB_ENV_VAR = "SPECKLE_DUCKDB_RECYCLE_MB";
  private const string DEFER_PK_ENV_VAR = "SPECKLE_DUCKDB_DEFER_PK";

  /// <summary>
  /// Caps each DuckDB instance's buffer pool (viewer + eav are separate
  /// files, so the DuckDB budget is 2× this during appends). Without a limit
  /// DuckDB defaults to 80% of machine RAM and keeps every written block
  /// cached until Complete() — the whole dataset rides in RAM.
  /// Env: <c>SPECKLE_DUCKDB_MEMORY_LIMIT_MB</c>.
  /// </summary>
  public int MemoryLimitMb { get; init; } = 256;

  /// <summary>
  /// CREATE INDEX / ADD PRIMARY KEY over tens of millions of rows needs more
  /// than the append budget (the ART index is built and checkpointed in
  /// memory) — connections are raised to this for their index phase only.
  /// 512MB OOMs on a 29M-row table.
  /// Env: <c>SPECKLE_DUCKDB_INDEX_MEMORY_LIMIT_MB</c>.
  /// </summary>
  public int IndexMemoryLimitMb { get; init; } = 1024;

  /// <summary>
  /// Recycle the appenders after this many unique objects. 0 disables the
  /// count trigger. Env: <c>SPECKLE_DUCKDB_RECYCLE_OBJECTS</c>.
  /// </summary>
  public int RecycleMaxObjects { get; init; } = 25_000;

  /// <summary>
  /// Recycle the appenders after roughly this many appended bytes (object
  /// JSON + blob bytes + EAV strings). The byte trigger exists because the
  /// object count is blind to row size: one blob row can be MBs and one
  /// DataObject can fan out into hundreds of EAV rows — and appender buffers
  /// live OUTSIDE the duckdb memory_limit (plain-allocator
  /// ColumnDataCollection: untracked, unevictable), so recycling is the only
  /// bound on that memory. 0 disables the byte trigger (with both triggers 0
  /// the writer flushes only at Complete() — the pre-recycling baseline).
  /// Env: <c>SPECKLE_DUCKDB_RECYCLE_MB</c> (in MB).
  /// </summary>
  public long RecycleMaxBytes { get; init; } = 64 * 1024 * 1024;

  /// <summary>
  /// When true (default), the objects/blobs tables are created WITHOUT
  /// primary keys and the PKs are added in Complete(): appends pay no
  /// per-row ART maintenance and the growing index doesn't compete with
  /// table data inside the append budget (uniqueness is already guaranteed
  /// upstream by the writer's dedup set). Final schema is identical either
  /// way. Env: <c>SPECKLE_DUCKDB_DEFER_PK</c> (0/false to disable).
  /// </summary>
  public bool DeferPrimaryKeys { get; init; } = true;

  public static DuckDbWriterOptions FromEnvironment()
  {
    var defaults = new DuckDbWriterOptions();
    return new DuckDbWriterOptions
    {
      MemoryLimitMb = ResolvePositiveInt(MEMORY_LIMIT_MB_ENV_VAR, defaults.MemoryLimitMb),
      IndexMemoryLimitMb = ResolvePositiveInt(INDEX_MEMORY_LIMIT_MB_ENV_VAR, defaults.IndexMemoryLimitMb),
      RecycleMaxObjects = ResolveNonNegativeInt(RECYCLE_OBJECTS_ENV_VAR, defaults.RecycleMaxObjects),
      RecycleMaxBytes =
        ResolveNonNegativeInt(RECYCLE_MB_ENV_VAR, (int)(defaults.RecycleMaxBytes / (1024 * 1024))) * 1024L * 1024L,
      DeferPrimaryKeys = ResolveBool(DEFER_PK_ENV_VAR, defaults.DeferPrimaryKeys),
    };
  }

  private static int ResolvePositiveInt(string name, int fallback) =>
    TryParse(name, out var value) && value > 0 ? value : fallback;

  // 0 is meaningful here (= trigger disabled), unlike the memory limits.
  private static int ResolveNonNegativeInt(string name, int fallback) =>
    TryParse(name, out var value) && value >= 0 ? value : fallback;

  private static bool TryParse(string name, out int value) =>
    int.TryParse(
      Environment.GetEnvironmentVariable(name),
      NumberStyles.Integer,
      CultureInfo.InvariantCulture,
      out value
    );

  private static bool ResolveBool(string name, bool fallback)
  {
    var raw = Environment.GetEnvironmentVariable(name);
    return raw switch
    {
      "0" => false,
      "1" => true,
      _ => bool.TryParse(raw, out var parsed) ? parsed : fallback,
    };
  }
}
#endif
