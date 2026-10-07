using AwesomeAssertions;
using DuckDB.NET.Data;
using Microsoft.Extensions.Logging.Abstractions;
using Speckle.Sdk.BundleMigrator.Migration;
using Speckle.Sdk.Pipelines.Receive.Artifacts;
using Speckle.Sdk.Pipelines.Send.Artifacts;

namespace Speckle.Sdk.BundleMigrator.Tests;

/// <summary>
/// The migrator's eav post-pass must leave a bundle that native consumers read as sorted (path-ordered eav plus
/// path_stats), carry exactly the same rows, and on any failure leave the produced bundle untouched.
/// </summary>
public class EavSortPostPassTests : IDisposable
{
  private const string BASE = "v1";
  private readonly string _dir = Path.Combine(Path.GetTempPath(), "SpeckleEavSort", Guid.NewGuid().ToString("N"));
  private readonly EavSortPostPass _pass = new(NullLogger<EavSortPostPass>.Instance);

  public void Dispose()
  {
    Environment.SetEnvironmentVariable(EavSortPostPass.KILL_SWITCH_ENV_VAR, null);
    if (Directory.Exists(_dir))
    {
      Directory.Delete(_dir, recursive: true);
    }
  }

  private string File(string table) => Path.Combine(_dir, $"{BASE}.eav.{table}.parquet");

  private static EavRow Row(string id, string path, string? text, double? num = null, string type = "string") =>
    new(id, path, text!, num, type, null, null);

  // Object-ordered, as the migrator writes it: each object's rows together, paths interleaved across objects.
  private void WriteBundle()
  {
    using var scheduler = new ParquetWriteScheduler();
    using var writer = new EavWriter(_dir, BASE, scheduler);
    for (int i = 0; i < 50; i++)
    {
      string id = $"obj-{i}";
      writer.AddRows(
        id,
        [
          Row(id, "name", $"Beam {i}"),
          Row(id, "properties.Length", null, i * 1.5, "number"),
          Row(id, "level", i % 2 == 0 ? "L1" : "L2"),
        ]
      );
      writer.AddType(
        id,
        i % 2 == 0 ? "UB" : "UC",
        () => [Row(id, "properties.Type.Mass", null, i % 2 == 0 ? 10 : 20, "number")]
      );
    }
    writer.Complete();
  }

  private static List<object?[]> Rows(string sql)
  {
    using var connection = new DuckDBConnection("DataSource=:memory:");
    connection.Open();
    using var command = connection.CreateCommand();
#pragma warning disable CA2100 // test-built SQL over temp files
    command.CommandText = sql;
#pragma warning restore CA2100
    using var reader = command.ExecuteReader();
    var rows = new List<object?[]>();
    while (reader.Read())
    {
      var row = new object?[reader.FieldCount];
      for (int i = 0; i < row.Length; i++)
      {
        row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
      }
      rows.Add(row);
    }
    return rows;
  }

  private static List<string> FileOrder(string file, string columns) =>
    Rows($"SELECT {columns} FROM read_parquet('{file}')").Select(r => string.Join("|", r)).ToList();

  private static List<string> Multiset(string file) =>
    FileOrder(file, "*").OrderBy(x => x, StringComparer.Ordinal).ToList();

  [Fact]
  public void Sorts_eav_and_type_eav_by_path_and_keeps_every_row()
  {
    WriteBundle();
    var eavBefore = Multiset(File("eav"));
    var typeEavBefore = Multiset(File("type_eav"));
    FileOrder(File("eav"), "path_index").Should().NotBeInAscendingOrder();

    _pass.Run(_dir, BASE).Should().BeTrue();

    var eavKeys = Rows($"SELECT path_index, object_index FROM read_parquet('{File("eav")}')")
      .Select(r => (Convert.ToInt32(r[0]), Convert.ToInt32(r[1])))
      .ToList();
    eavKeys.Should().BeInAscendingOrder();
    Multiset(File("eav")).Should().Equal(eavBefore);
    Multiset(File("type_eav")).Should().Equal(typeEavBefore);
    Directory.Exists(Path.Combine(_dir, EavSortPostPass.STAGING_DIR)).Should().BeFalse();
  }

  [Fact]
  public void Writes_path_stats_counting_type_params_through_object_type()
  {
    WriteBundle();

    _pass.Run(_dir, BASE).Should().BeTrue();

    var stats = Rows(
        $"SELECT p.path, s.object_count, s.n_string, s.n_double, s.distinct_strings, s.min_double, s.max_double "
          + $"FROM read_parquet('{File("path_stats")}') s JOIN read_parquet('{File("paths")}') p USING (path_index)"
      )
      .ToDictionary(r => (string)r[0]!, r => r);
    stats["level"][1].Should().Be(50);
    stats["level"][4].Should().Be(2);
    stats["properties.Length"][3].Should().Be(50);
    stats["properties.Length"][6].Should().Be(49 * 1.5);
    // Two types, but the stats are per object: every object resolves its type's mass.
    stats["properties.Type.Mass"][1].Should().Be(50);
    stats["properties.Type.Mass"][5].Should().Be(10d);
    stats["properties.Type.Mass"][6].Should().Be(20d);
  }

  [Fact]
  public async Task Sorted_bundle_reads_back_through_the_sdk_receive_path()
  {
    WriteBundle();

    _pass.Run(_dir, BASE).Should().BeTrue();

    var table = PropertyTable.Load(
      await ParquetTableReader.ReadAsync(File("eav")),
      await ParquetTableReader.ReadAsync(File("paths")),
      "object_index"
    );
    table.KeyCount.Should().Be(50);
    table.GetString(7, "name").Should().Be("Beam 7");
    table.GetDouble(7, "properties.Length").Should().Be(10.5);
    table.GetString(7, "level").Should().Be("L2");
  }

  [Fact]
  public void Kill_switch_leaves_the_bundle_untouched()
  {
    WriteBundle();
    var before = System.IO.File.ReadAllBytes(File("eav"));
    Environment.SetEnvironmentVariable(EavSortPostPass.KILL_SWITCH_ENV_VAR, "0");

    _pass.Run(_dir, BASE).Should().BeFalse();

    System.IO.File.ReadAllBytes(File("eav")).Should().Equal(before);
    System.IO.File.Exists(File("path_stats")).Should().BeFalse();
  }

  [Fact]
  public void Failure_leaves_the_produced_files_and_no_staging()
  {
    WriteBundle();
    var eavBefore = System.IO.File.ReadAllBytes(File("eav"));
    System.IO.File.WriteAllText(File("type_eav"), "not a parquet file");

    _pass.Run(_dir, BASE).Should().BeFalse();

    System.IO.File.ReadAllBytes(File("eav")).Should().Equal(eavBefore);
    System.IO.File.Exists(File("path_stats")).Should().BeFalse();
    Directory.Exists(Path.Combine(_dir, EavSortPostPass.STAGING_DIR)).Should().BeFalse();
    Directory.GetFiles(_dir).Should().OnlyContain(f => f.EndsWith(".parquet", StringComparison.Ordinal));
  }

  [Fact]
  public void Clears_staging_left_by_a_killed_run()
  {
    WriteBundle();
    var stale = Path.Combine(_dir, EavSortPostPass.STAGING_DIR);
    Directory.CreateDirectory(stale);
    System.IO.File.WriteAllText(Path.Combine(stale, $"{BASE}.eav.eav.parquet"), "half-written");

    _pass.Run(_dir, BASE).Should().BeTrue();

    Directory.Exists(stale).Should().BeFalse();
  }

  [Fact]
  public void Skips_a_bundle_without_eav()
  {
    Directory.CreateDirectory(_dir);

    _pass.Run(_dir, BASE).Should().BeFalse();
  }

  private const long GIB = 1024L * 1024 * 1024;

  [Theory]
  [InlineData(16 * GIB, 4 * GIB, 9 * GIB)] // 12 GiB left: 3/4 of it, under the 1 GiB headroom cap
  [InlineData(4 * GIB, 1 * GIB, 2 * GIB)] // 3 GiB left: headroom cap (left - 1 GiB) wins over 3/4
  public void Sizes_duckdb_from_memory_the_pod_has_left(long max, long current, long expected) =>
    EavSortPostPass.MemoryBudgetBytes(max, current).Should().Be(expected);

  [Fact]
  public void Skips_when_the_pod_has_too_little_memory_left() =>
    EavSortPostPass.MemoryBudgetBytes(4 * GIB, 3 * GIB).Should().BeNull();

  [Fact]
  public void Uses_a_fixed_budget_off_cgroup() => EavSortPostPass.MemoryBudgetBytes(null, null).Should().Be(4 * GIB);
}
