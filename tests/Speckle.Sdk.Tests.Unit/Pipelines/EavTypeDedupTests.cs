#if NET8_0_OR_GREATER
using AwesomeAssertions;
using DuckDB.NET.Data;
using Speckle.Sdk.Pipelines.Send.Artifacts;

namespace Speckle.Sdk.Tests.Unit.Pipelines;

/// <summary>
/// Type-parameter normalization (SOT §6): type params dedup into <c>type_eav</c> (once per type),
/// objects carry a weak ref via <c>object_type</c>, and the <c>object_properties</c> view re-flattens
/// instance ∪ type so the old flat contract holds. The lazy factory must run once per type, not per
/// instance.
/// </summary>
public sealed class EavTypeDedupTests : IDisposable
{
  private readonly string _dir = Path.Combine(Path.GetTempPath(), "speckle-eavtype-" + Guid.NewGuid().ToString("N"));

  [Fact]
  public void TypeParams_DedupedOncePerType_ViewReflattens()
  {
    string dbPath;
    int t1FlattenCalls = 0;

    using (var w = new EavWriter(_dir, "model"))
    {
      dbPath = w.EavDbPath;

      // A & B share type T1; C is type T2. Instance params live per-object in eav.
      w.AddRows("A", new[] { new EavRow("A", "properties.elementId", "1", 1, "number", null, null) });
      w.AddRows("B", new[] { new EavRow("B", "properties.elementId", "2", 2, "number", null, null) });
      w.AddRows("C", new[] { new EavRow("C", "properties.elementId", "3", 3, "number", null, null) });

      IEnumerable<EavRow> T1()
      {
        t1FlattenCalls++;
        return new[]
        {
          new EavRow("", "properties.Parameters.Type Parameters.Other.Type Mark", "W1", null, "string", null, "TYPE_MARK"),
        };
      }

      w.AddType("A", "T1", T1); // first sight → factory runs, type_eav written
      w.AddType("B", "T1", T1); // dedup → factory NOT run, just an object_type link
      w.AddType(
        "C",
        "T2",
        () => new[] { new EavRow("", "properties.Parameters.Type Parameters.Other.Type Mark", "W2", null, "string", null, "TYPE_MARK") }
      );

      w.Complete(); // builds the index + object_properties view (Dispose alone is the cleanup path)
    }

    t1FlattenCalls.Should().Be(1); // flattened once for T1, not per instance

    using var db = new DuckDBConnection($"Data Source={dbPath};ACCESS_MODE=READ_ONLY");
    db.Open();

    Scalar(db, "SELECT count(*) FROM types").Should().Be(2L); // T1, T2
    Scalar(db, "SELECT count(*) FROM type_eav").Should().Be(2L); // one Type Mark per type (deduped)
    Scalar(db, "SELECT count(*) FROM object_type").Should().Be(3L); // A, B, C each linked

    // Raw eav does NOT carry type params (the dedup) — only instance.
    Scalar(db, "SELECT count(*) FROM eav e JOIN paths p ON p.path_index=e.path_index WHERE p.path LIKE '%Type Mark'")
      .Should()
      .Be(0L);

    // The view re-flattens: A & B both resolve Type Mark = W1 (from T1); C resolves W2.
    Scalar(
        db,
        "SELECT count(*) FROM object_properties op JOIN paths p ON p.path_index=op.path_index WHERE p.path LIKE '%Type Mark' AND op.value_string='W1'"
      )
      .Should()
      .Be(2L);
    Scalar(
        db,
        "SELECT value_string FROM object_properties op JOIN objects o ON o.object_index=op.object_index JOIN paths p ON p.path_index=op.path_index WHERE o.application_id='C' AND p.path LIKE '%Type Mark'"
      )
      .Should()
      .Be("W2");
  }

  [System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "Test-controlled constant SQL."
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
