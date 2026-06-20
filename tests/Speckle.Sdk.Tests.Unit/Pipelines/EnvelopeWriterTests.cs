#if NET8_0_OR_GREATER
using AwesomeAssertions;
using DuckDB.NET.Data;
using Speckle.Sdk.Pipelines.Send.Artifacts;

namespace Speckle.Sdk.Tests.Unit.Pipelines;

/// <summary>
/// Round-trips the Speckle 4.0 envelope writer — now direct Zstd parquet (relations + nodes + catalog).
/// Reads the parquet files back via DuckDB's <c>read_parquet</c> and asserts the SOT §2/§6 shape.
/// </summary>
public sealed class EnvelopeWriterTests : IDisposable
{
  private readonly string _dir = Path.Combine(Path.GetTempPath(), "speckle-env-test-" + Guid.NewGuid().ToString("N"));

  [Fact]
  public void WritesRelationsAndNodes_RoundTrips()
  {
    using (var w = new EnvelopeWriter(_dir, "model"))
    {
      w.AddNode(0, NodeKind.Definition, "wall-def", null, null, null, null, null, null, null, null);
      w.AddNode(1, NodeKind.Instance, null, 0, "1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1", "mm", null, null, null, null, null);
      w.AddNode(2, NodeKind.Material, null, null, null, null, unchecked((int)0xFF8800FFu), 1.0, 0.0, 0.4, null);
      w.AddNode(3, NodeKind.Level, "Level 1", null, null, null, null, null, null, null, 3000.0);

      w.AddRelation(RelKind.DisplayInstance, 0, 1, 0);
      w.AddRelation(RelKind.Defines, 0, 5, 0);
      w.AddRelation(RelKind.HasMaterial, 5, 2, 0);
      w.AddRelation(RelKind.OnLevel, 0, 3, 0);
      w.AddRelation(RelKind.Subelement, 0, 9, 0);

      w.Complete();
    }

    using var db = new DuckDBConnection("Data Source=:memory:");
    db.Open();
    View(db, "relations");
    View(db, "nodes");
    View(db, "rel_types");
    View(db, "node_kinds");
    View(db, "meta");

    Scalar(db, "SELECT count(*) FROM relations").Should().Be(5L);
    Scalar(db, "SELECT count(*) FROM nodes").Should().Be(4L);
    Scalar(db, $"SELECT count(*) FROM relations WHERE rel = {RelKind.DisplayInstance}").Should().Be(1L);
    Scalar(db, $"SELECT count(*) FROM relations WHERE rel = {RelKind.HasMaterial}").Should().Be(1L);
    Scalar(db, $"SELECT def_ref FROM nodes WHERE kind = {NodeKind.Instance}").Should().Be(0);
    Scalar(db, $"SELECT transform FROM nodes WHERE kind = {NodeKind.Instance}")
      .Should()
      .Be("1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1");
    Scalar(db, $"SELECT elevation FROM nodes WHERE kind = {NodeKind.Level}").Should().Be(3000.0);

    // self-describing catalog (SOT §6)
    Scalar(db, "SELECT count(*) FROM rel_types").Should().Be(8L);
    Scalar(db, "SELECT count(*) FROM node_kinds").Should().Be(5L);
    Scalar(db, $"SELECT name FROM rel_types WHERE rel = {RelKind.DisplayInstance}").Should().Be("DISPLAY_INSTANCE");
    Scalar(db, $"SELECT src_ns FROM rel_types WHERE rel = {RelKind.HasMaterial}").Should().Be("geometry");
    Scalar(db, "SELECT schema_version FROM meta").Should().Be(1);
  }

  private void View(DuckDBConnection db, string table) =>
    Exec(db, $"CREATE VIEW {table} AS SELECT * FROM read_parquet('{_dir}/model.envelope.{table}.parquet')");

  [System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "Test-controlled constant SQL (table/path names are compile-time constants)."
  )]
  private static void Exec(DuckDBConnection db, string sql)
  {
    using var cmd = db.CreateCommand();
    cmd.CommandText = sql;
    cmd.ExecuteNonQuery();
  }

  [System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "Test-controlled constant SQL (rel/kind ids are compile-time constants)."
  )]
  private static object Scalar(DuckDBConnection db, string sql)
  {
    using var cmd = db.CreateCommand();
    cmd.CommandText = sql;
    return cmd.ExecuteScalar()!;
  }

  public void Dispose()
  {
    try
    {
      if (Directory.Exists(_dir))
      {
        Directory.Delete(_dir, recursive: true);
      }
    }
    catch (IOException)
    {
      // best-effort temp cleanup
    }
  }
}
#endif
