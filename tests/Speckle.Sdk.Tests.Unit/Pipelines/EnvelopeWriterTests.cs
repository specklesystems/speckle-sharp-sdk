#if NET8_0_OR_GREATER
using AwesomeAssertions;
using DuckDB.NET.Data;
using Speckle.Sdk.Pipelines.Send.Artifacts;

namespace Speckle.Sdk.Tests.Unit.Pipelines;

/// <summary>
/// Round-trips the Speckle 4.0 <c>envelope.duckdb</c> (relations + nodes) writer: write a
/// small topology graph, read it back, assert the schema/shape from
/// <c>notes/topology-envelope-SOT.md</c> (two tables, pure-int endpoints, no <c>proxies</c>).
/// </summary>
public sealed class EnvelopeWriterTests : IDisposable
{
  private readonly string _dir = Path.Combine(Path.GetTempPath(), "speckle-env-test-" + Guid.NewGuid().ToString("N"));

  [Fact]
  public void WritesRelationsAndNodes_RoundTrips()
  {
    string dbPath;

    using (var w = new EnvelopeWriter(_dir, "model"))
    {
      dbPath = w.EnvelopeDbPath;

      // nodes: one DEFINITION, one INSTANCE (with transform), one MATERIAL, one LEVEL.
      w.AddNode(0, NodeKind.Definition, "wall-def", null, null, null, null, null, null, null, null);
      w.AddNode(1, NodeKind.Instance, null, 0, "1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1", "mm", null, null, null, null, null);
      w.AddNode(2, NodeKind.Material, null, null, null, null, unchecked((int)0xFF8800FFu), 1.0, 0.0, 0.4, null);
      w.AddNode(3, NodeKind.Level, "Level 1", null, null, null, null, null, null, null, 3000.0);

      // relations: object0 DISPLAY_INSTANCE instance1 ; def0 DEFINES geom5 ; geom5 HAS_MATERIAL mat2 ;
      // object0 ON_LEVEL level3 ; wall0 SUBELEMENT panel9.
      w.AddRelation(RelKind.DisplayInstance, 0, 1, 0);
      w.AddRelation(RelKind.Defines, 0, 5, 0);
      w.AddRelation(RelKind.HasMaterial, 5, 2, 0);
      w.AddRelation(RelKind.OnLevel, 0, 3, 0);
      w.AddRelation(RelKind.Subelement, 0, 9, 0);
    }

    using var db = new DuckDBConnection($"Data Source={dbPath};ACCESS_MODE=READ_ONLY");
    db.Open();

    Scalar(db, "SELECT count(*) FROM relations").Should().Be(5L);
    Scalar(db, "SELECT count(*) FROM nodes").Should().Be(4L);

    // rel-by-kind
    Scalar(db, $"SELECT count(*) FROM relations WHERE rel = {RelKind.DisplayInstance}").Should().Be(1L);
    Scalar(db, $"SELECT count(*) FROM relations WHERE rel = {RelKind.HasMaterial}").Should().Be(1L);

    // an INSTANCE node carries its def_ref + transform; a LEVEL carries elevation
    Scalar(db, $"SELECT def_ref FROM nodes WHERE kind = {NodeKind.Instance}").Should().Be(0);
    Scalar(db, $"SELECT transform FROM nodes WHERE kind = {NodeKind.Instance}")
      .Should()
      .Be("1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1");
    Scalar(db, $"SELECT elevation FROM nodes WHERE kind = {NodeKind.Level}").Should().Be(3000.0);

    // schema invariants: exactly relations+nodes, no legacy proxies table; int endpoints
    Scalar(db, "SELECT count(*) FROM information_schema.tables WHERE table_name = 'proxies'").Should().Be(0L);
    Scalar(db, "SELECT count(*) FROM information_schema.tables WHERE table_name IN ('relations','nodes')")
      .Should()
      .Be(2L);
    Scalar(db, "SELECT data_type FROM information_schema.columns WHERE table_name='relations' AND column_name='src'")
      .Should()
      .Be("INTEGER");

    // self-describing catalog (SOT §6): rel/kind vocabulary travels in the artefact
    Scalar(db, "SELECT count(*) FROM rel_types").Should().Be(8L);
    Scalar(db, "SELECT count(*) FROM node_kinds").Should().Be(5L);
    Scalar(db, $"SELECT name FROM rel_types WHERE rel = {RelKind.DisplayInstance}").Should().Be("DISPLAY_INSTANCE");
    Scalar(db, $"SELECT src_ns FROM rel_types WHERE rel = {RelKind.HasMaterial}").Should().Be("geometry");
    Scalar(db, "SELECT schema_version FROM meta").Should().Be(1);
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
