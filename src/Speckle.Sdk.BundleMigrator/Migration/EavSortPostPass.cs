using DuckDB.NET.Data;
using Microsoft.Extensions.Logging;

namespace Speckle.Sdk.BundleMigrator.Migration;

/// <summary>
/// Re-sorts a produced bundle's <c>eav</c> by (path_index, object_index) and <c>type_eav</c> by
/// (path_index, type_index), and writes the <c>path_stats</c> sidecar — the same post-pass native converter
/// bundles get (speckle-converters <c>dispatch/src/dispatch/postpass.py</c>), with the same contract:
/// <list type="bullet">
/// <item>Best-effort: any failure leaves the bundle exactly as produced (object-ordered, valid, slow) and never
/// fails the migration.</item>
/// <item>Verified: each sorted file must match its source on row count and an order-insensitive hash over every
/// column before it replaces anything.</item>
/// <item>Ship-together: consumers read <c>path_stats</c> present as "eav is sorted". Files are swapped in the
/// order path_stats → eav → type_eav and nothing already swapped is ever rolled back, so every stopping point
/// leaves either stats + unsorted eav (valid) or stats + sorted eav — never sorted eav without stats.</item>
/// </list>
/// </summary>
internal sealed class EavSortPostPass(ILogger<EavSortPostPass> logger)
{
  /// <summary>Set to <c>0</c> to skip the pass (rollout insurance, mirrors <c>DISPATCH_EAV_SORT</c>).</summary>
  public const string KILL_SWITCH_ENV_VAR = "SPECKLE_MIGRATOR_EAV_SORT";

  // A subdirectory: both upload paths and the migration service list only the bundle directory's top-level
  // files, so staged and spilled files can never be uploaded or reported as bundle content.
  internal const string STAGING_DIR = ".eav-sort";

  private const long GIB = 1024L * 1024 * 1024;

  // The migrator still holds the deserialised graph while this runs, so DuckDB is sized from what the pod has
  // left, not from its whole limit: an OOM kill would fail the migration, which the best-effort contract forbids.
  private const long MIN_BUDGET_BYTES = GIB;
  private const long HEADROOM_BYTES = GIB;
  private const long OFF_CGROUP_BUDGET_BYTES = 4 * GIB;

  /// <summary>Runs the pass over the bundle <paramref name="baseName"/> in <paramref name="bundleDir"/>.
  /// Returns true when the sorted files and path_stats were swapped in.</summary>
  public bool Run(string bundleDir, string baseName)
  {
    if (Environment.GetEnvironmentVariable(KILL_SWITCH_ENV_VAR) == "0")
    {
      logger.LogInformation("eav sort post-pass disabled ({EnvVar}=0)", KILL_SWITCH_ENV_VAR);
      return false;
    }

    var staging = Path.Combine(bundleDir, STAGING_DIR);
    var started = System.Diagnostics.Stopwatch.StartNew();
    try
    {
      DeleteStaging(staging);
      if (!RunCore(bundleDir, baseName, staging))
      {
        return false;
      }
      logger.LogInformation("eav sort post-pass done in {Seconds:F1}s", started.Elapsed.TotalSeconds);
      return true;
    }
#pragma warning disable CA1031 // best-effort by contract: the produced bundle ships object-ordered instead
    catch (Exception ex)
#pragma warning restore CA1031
    {
      logger.LogWarning(ex, "eav sort post-pass failed; shipping the bundle object-ordered");
      return false;
    }
    finally
    {
      DeleteStaging(staging);
    }
  }

  private bool RunCore(string bundleDir, string baseName, string staging)
  {
    string eav = BundlePath(bundleDir, baseName, "eav");
    string typeEav = BundlePath(bundleDir, baseName, "type_eav");
    string objectType = BundlePath(bundleDir, baseName, "object_type");
    string pathStats = BundlePath(bundleDir, baseName, "path_stats");
    if (!File.Exists(eav))
    {
      logger.LogInformation("eav sort post-pass: no {File}, skipping", Path.GetFileName(eav));
      return false;
    }

    long? budget = MemoryBudgetBytes(CgroupBytes("memory.max"), CgroupBytes("memory.current"));
    if (budget is null)
    {
      logger.LogWarning(
        "eav sort post-pass: under {MinGiB} GiB of memory left in the pod, skipping",
        MIN_BUDGET_BYTES / GIB
      );
      return false;
    }

    Directory.CreateDirectory(staging);
    var staged = new List<(string Tmp, string Final)>();
    string stagedStats = Path.Combine(staging, Path.GetFileName(pathStats));

    using (var connection = new DuckDBConnection("DataSource=:memory:"))
    {
      connection.Open();
      Execute(connection, $"SET memory_limit='{budget.Value / (1024 * 1024)}MB'");
      Execute(connection, $"SET temp_directory='{Quote(Path.Combine(staging, "spill"))}'");
      Execute(connection, "SET preserve_insertion_order=false");
      if (CgroupCpuThreads() is { } threads)
      {
        Execute(connection, $"SET threads={threads}");
      }

      foreach (var (source, order) in new[] { (eav, "path_index, object_index"), (typeEav, "path_index, type_index") })
      {
        if (!File.Exists(source))
        {
          continue;
        }
        string tmp = Path.Combine(staging, Path.GetFileName(source));
        // PARQUET_VERSION V2 is required, not a tweak: under V1 object_index dictionary-overflows and the sorted
        // file grows instead of shrinking (postpass.py, measured on a 274M-row bundle).
        Execute(
          connection,
          $"COPY (SELECT * FROM read_parquet('{Quote(source)}') ORDER BY {order}) "
            + $"TO '{Quote(tmp)}' (FORMAT parquet, COMPRESSION zstd, PARQUET_VERSION V2)"
        );
        VerifyEquivalent(connection, source, tmp);
        logger.LogInformation(
          "eav sort post-pass: {File} {Before} -> {After} bytes",
          Path.GetFileName(source),
          new FileInfo(source).Length,
          new FileInfo(tmp).Length
        );
        staged.Add((tmp, source));
      }

      // Reads the original files: the stats are order-independent and the sources are untouched until the swap.
      Execute(
        connection,
        $"COPY ({PathStatsSql(eav, typeEav, objectType)}) "
          + $"TO '{Quote(stagedStats)}' (FORMAT parquet, COMPRESSION zstd, PARQUET_VERSION V2)"
      );
    }

    File.Move(stagedStats, pathStats, overwrite: true);
    foreach (var (tmp, final) in staged)
    {
      File.Move(tmp, final, overwrite: true);
    }
    return true;
  }

  /// <summary>
  /// Per-path aggregates over the object_properties population — instance eav plus type params resolved through
  /// object_type — so type-scoped parameters are counted. Same schema as the native sidecar.
  /// </summary>
  internal static string PathStatsSql(string eav, string typeEav, string objectType)
  {
    const string COLUMNS = "object_index, path_index, value_string, value_double, value_boolean";
    string population = $"SELECT {COLUMNS} FROM read_parquet('{Quote(eav)}')";
    if (File.Exists(typeEav) && File.Exists(objectType))
    {
      population +=
        " UNION ALL SELECT ot.object_index, te.path_index, te.value_string, te.value_double, te.value_boolean "
        + $"FROM read_parquet('{Quote(objectType)}') ot "
        + $"JOIN read_parquet('{Quote(typeEav)}') te ON te.type_index = ot.type_index";
    }
    return "SELECT path_index, "
      + "count(DISTINCT object_index)::INT AS object_count, "
      + "count(value_string)::INT AS n_string, "
      + "count(value_double)::INT AS n_double, "
      + "count(value_boolean)::INT AS n_boolean, "
      + "count(DISTINCT value_string)::INT AS distinct_strings, "
      + "min(value_double) AS min_double, "
      + "max(value_double) AS max_double "
      + $"FROM ({population}) GROUP BY path_index ORDER BY path_index";
  }

  /// <summary>
  /// DuckDB memory_limit in bytes: three quarters of what the pod has left, keeping at least 1 GiB of headroom
  /// for the migrator itself. Null when that leaves less than 1 GiB. Off-cgroup (local runs) uses a fixed 4 GiB.
  /// </summary>
  internal static long? MemoryBudgetBytes(long? memoryMax, long? memoryCurrent)
  {
    if (memoryMax is not { } max)
    {
      return OFF_CGROUP_BUDGET_BYTES;
    }
    long available = max - (memoryCurrent ?? 0);
    long budget = Math.Min(available * 3 / 4, available - HEADROOM_BYTES);
    return budget >= MIN_BUDGET_BYTES ? budget : null;
  }

  private static void VerifyEquivalent(DuckDBConnection connection, string source, string sorted)
  {
    var columns = new List<string>();
    using (var describe = Command(connection, $"DESCRIBE SELECT * FROM read_parquet('{Quote(source)}')"))
    {
      using var reader = describe.ExecuteReader();
      while (reader.Read())
      {
        columns.Add($"\"{reader.GetString(0).Replace("\"", "\"\"")}\"");
      }
    }
    string fingerprint = $"SELECT count(*), bit_xor(hash({string.Join(", ", columns)})) FROM read_parquet('{{0}}')";
    var before = Fingerprint(connection, string.Format(fingerprint, Quote(source)));
    var after = Fingerprint(connection, string.Format(fingerprint, Quote(sorted)));
    if (before != after)
    {
      throw new InvalidOperationException(
        $"Sorted rewrite of {Path.GetFileName(source)} is not content-identical (rows {before.Rows} -> {after.Rows})"
      );
    }
  }

  private static (long Rows, string Hash) Fingerprint(DuckDBConnection connection, string sql)
  {
    using var command = Command(connection, sql);
    using var reader = command.ExecuteReader();
    reader.Read();
    return (Convert.ToInt64(reader.GetValue(0)), Convert.ToString(reader.GetValue(1)) ?? "");
  }

  private static void Execute(DuckDBConnection connection, string sql)
  {
    using var command = Command(connection, sql);
    command.ExecuteNonQuery();
  }

  private static DuckDBCommand Command(DuckDBConnection connection, string sql)
  {
    var command = connection.CreateCommand();
    // The only interpolated values are file paths under the migrator's own output directory, quoted by Quote().
#pragma warning disable CA2100
    command.CommandText = sql;
#pragma warning restore CA2100
    return command;
  }

  private static string BundlePath(string dir, string baseName, string table) =>
    Path.Combine(dir, $"{baseName}.eav.{table}.parquet");

  private static string Quote(string value) => value.Replace("'", "''");

  private static long? CgroupBytes(string file)
  {
    try
    {
      string raw = File.ReadAllText(Path.Combine("/sys/fs/cgroup", file)).Trim();
      return long.TryParse(raw, out var bytes) ? bytes : null;
    }
    catch (IOException)
    {
      return null;
    }
    catch (UnauthorizedAccessException)
    {
      return null;
    }
  }

  // Caps DuckDB at the pod's CPU quota so it doesn't start one thread per host core on a CFS-limited pod.
  private static int? CgroupCpuThreads()
  {
    try
    {
      var parts = File.ReadAllText("/sys/fs/cgroup/cpu.max").Split(' ', StringSplitOptions.RemoveEmptyEntries);
      if (parts.Length == 2 && long.TryParse(parts[0], out var quota) && long.TryParse(parts[1], out var period))
      {
        return (int)Math.Max(1, Math.Ceiling(quota / (double)period));
      }
    }
    catch (IOException) { }
    catch (UnauthorizedAccessException) { }
    return null;
  }

  private void DeleteStaging(string staging)
  {
    try
    {
      if (Directory.Exists(staging))
      {
        Directory.Delete(staging, recursive: true);
      }
    }
    catch (IOException ex)
    {
      logger.LogWarning(ex, "eav sort post-pass: could not remove {Staging}", staging);
    }
  }
}
