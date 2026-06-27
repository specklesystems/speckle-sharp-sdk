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
    using var scheduler = new ParquetWriteScheduler();
    using (var w = new EnvelopeWriter(_dir, "model", scheduler))
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
    scheduler.CompleteAndWait(); // drain the background writer so the parquet files are on disk

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
    Scalar(db, "SELECT count(*) FROM rel_types").Should().Be(22L); // 11 + cross-connector topology vocab (IN_ROOM..HOSTED_ON)
    Scalar(db, "SELECT count(*) FROM node_kinds").Should().Be(7L); // 6 + CONTAINER (new IN_* rels all reuse CONTAINER)
    Scalar(db, $"SELECT name FROM rel_types WHERE rel = {RelKind.DisplayInstance}").Should().Be("DISPLAY_INSTANCE");
    Scalar(db, $"SELECT name FROM rel_types WHERE rel = {RelKind.DefinesInstance}").Should().Be("DEFINES_INSTANCE");
    // DEFINES (4) is now geometry-only; DEFINES_INSTANCE (9) carries node→node nesting. rel fixes dst namespace.
    Scalar(db, $"SELECT dst_ns FROM rel_types WHERE rel = {RelKind.Defines}").Should().Be("geometry");
    Scalar(db, $"SELECT dst_ns FROM rel_types WHERE rel = {RelKind.DefinesInstance}").Should().Be("node");
    Scalar(db, $"SELECT src_ns FROM rel_types WHERE rel = {RelKind.HasMaterial}").Should().Be("geometry");
    // IN_MODEL (11) → CONTAINER node; the default-projection top key (SOT §8).
    Scalar(db, $"SELECT name FROM rel_types WHERE rel = {RelKind.InModel}").Should().Be("IN_MODEL");
    Scalar(db, $"SELECT dst_ns FROM rel_types WHERE rel = {RelKind.InModel}").Should().Be("node");
    Scalar(db, $"SELECT name FROM node_kinds WHERE kind = {NodeKind.Container}").Should().Be("CONTAINER");
    // cross-connector topology vocab (ENG-8693): IN_* membership → CONTAINER (object→node); the two graph
    // edges (CONNECTS_TO / HOSTED_ON) are object→object — rel fixes the dst namespace.
    Scalar(db, $"SELECT name FROM rel_types WHERE rel = {RelKind.InSystem}").Should().Be("IN_SYSTEM");
    Scalar(db, $"SELECT dst_ns FROM rel_types WHERE rel = {RelKind.InNetwork}").Should().Be("node");
    Scalar(db, $"SELECT name FROM rel_types WHERE rel = {RelKind.ConnectsTo}").Should().Be("CONNECTS_TO");
    Scalar(db, $"SELECT dst_ns FROM rel_types WHERE rel = {RelKind.ConnectsTo}").Should().Be("object");
    Scalar(db, $"SELECT dst_ns FROM rel_types WHERE rel = {RelKind.HostedOn}").Should().Be("object");
    Scalar(db, "SELECT schema_version FROM meta").Should().Be(3);

    // No scene views authored ⇒ the table is absent (consumer feature-detects by file presence).
    File.Exists(Path.Combine(_dir, "model.envelope.scene_views.parquet")).Should().BeFalse();
  }

  [Fact]
  public void WritesSceneViews_RoundTrips()
  {
    using var scheduler = new ParquetWriteScheduler();
    using (var w = new EnvelopeWriter(_dir, "model", scheduler))
    {
      // Revit default: IN_MODEL → ON_LEVEL → category (a rel/rel/eav stack), plus a named eav-only alternate.
      w.AddSceneView(
        new SceneView(
          0,
          "Default",
          IsDefault: true,
          new[] { SceneViewKey.Rel(RelKind.InModel), SceneViewKey.Rel(RelKind.OnLevel), SceneViewKey.Eav("category") }
        )
      );
      w.AddSceneView(new SceneView(1, "By Category", IsDefault: false, new[] { SceneViewKey.Eav("category") }));
      w.Complete();
    }
    scheduler.CompleteAndWait();

    using var db = new DuckDBConnection("Data Source=:memory:");
    db.Open();
    View(db, "scene_views");

    Scalar(db, "SELECT count(*) FROM scene_views").Should().Be(4L); // 3 tiers + 1 tier
    Scalar(db, "SELECT count(DISTINCT view) FROM scene_views").Should().Be(2L);
    Scalar(db, "SELECT is_default FROM scene_views WHERE view = 0 LIMIT 1").Should().Be(true);
    Scalar(db, "SELECT is_default FROM scene_views WHERE view = 1 LIMIT 1").Should().Be(false);

    // ord 0 is a rel walk over IN_MODEL; ref is the rel code as int-string (joins rel_types).
    Scalar(db, "SELECT source FROM scene_views WHERE view = 0 AND ord = 0").Should().Be("rel");
    Scalar(db, "SELECT ref FROM scene_views WHERE view = 0 AND ord = 0").Should().Be(((int)RelKind.InModel).ToString());
    // ord 2 is an eav group-by; ref is the bare attr key.
    Scalar(db, "SELECT source FROM scene_views WHERE view = 0 AND ord = 2").Should().Be("eav");
    Scalar(db, "SELECT ref FROM scene_views WHERE view = 0 AND ord = 2").Should().Be("category");
    Scalar(db, "SELECT name FROM scene_views WHERE view = 1 LIMIT 1").Should().Be("By Category");
  }

  // The Revit default projection rule (mirrors the ODA RevitModelExtractor authoring): the domain default is
  // model → level → category → family, but the IN_MODEL tier is conditional — present only when the file has
  // more than one source model (linked models); a single-model file groups by level first.
  [Theory]
  [InlineData(2, 4, 1L)] // multi-model: IN_MODEL prepended ⇒ 4 tiers, IN_MODEL at ord 0
  [InlineData(1, 3, 0L)] // single-model: no model axis ⇒ 3 tiers, no IN_MODEL
  public void RevitDefaultProjection_PrependsModelTier_OnlyWhenMultiModel(int modelCount, long tiers, long modelAtOrd0)
  {
    using var scheduler = new ParquetWriteScheduler();
    using (var w = new EnvelopeWriter(_dir, "model", scheduler))
    {
      w.AddSceneView(new SceneView(0, "Default", IsDefault: true, RevitDefaultKeys(modelCount)));
      w.Complete();
    }
    scheduler.CompleteAndWait();

    using var db = new DuckDBConnection("Data Source=:memory:");
    db.Open();
    View(db, "scene_views");

    Scalar(db, "SELECT count(*) FROM scene_views WHERE view = 0").Should().Be(tiers);
    Scalar(db, $"SELECT count(*) FROM scene_views WHERE ord = 0 AND source = 'rel' AND ref = '{(int)RelKind.InModel}'")
      .Should()
      .Be(modelAtOrd0);
    // level → category → family always trail (level via rel, category/family via eav).
    Scalar(db, $"SELECT ref FROM scene_views WHERE ord = {tiers - 3}").Should().Be(((int)RelKind.OnLevel).ToString());
    Scalar(db, $"SELECT ref FROM scene_views WHERE ord = {tiers - 1}").Should().Be("family");
  }

  // Reference implementation of the rule — drop the identical body into ODA RevitModelExtractor (modelCount =
  // distinct source documents: host + linked models; 1 today until linked-model traversal is built).
  private static List<SceneViewKey> RevitDefaultKeys(int modelCount)
  {
    var keys = new List<SceneViewKey>();
    if (modelCount > 1)
    {
      keys.Add(SceneViewKey.Rel(RelKind.InModel));
    }
    keys.Add(SceneViewKey.Rel(RelKind.OnLevel));
    keys.Add(SceneViewKey.Eav("category"));
    keys.Add(SceneViewKey.Eav("family"));
    return keys;
  }

  // The Navis default projection rule (mirrors the ODA NavisModelBinaryExtractor authoring): a federated
  // file groups by source model (IN_MODEL) — but only when it federates >1 model. A single-model file has
  // no model axis (and Navis has no uniform level/category), so NO default is authored ⇒ consumer falls back.
  [Theory]
  [InlineData(3, 1L, 1L)] // federated (>1 model): [IN_MODEL] ⇒ 1 tier, IN_MODEL at ord 0
  [InlineData(1, 0L, 0L)] // single model: nothing authored ⇒ scene_views file absent
  public void NavisDefaultProjection_GroupsBySourceModel_OnlyWhenFederated(int modelCount, long tiers, long modelAtOrd0)
  {
    using var scheduler = new ParquetWriteScheduler();
    using (var w = new EnvelopeWriter(_dir, "model", scheduler))
    {
      var keys = NavisDefaultKeys(modelCount);
      if (keys.Count > 0)
      {
        w.AddSceneView(new SceneView(0, "Default", IsDefault: true, keys));
      }
      w.Complete();
    }
    scheduler.CompleteAndWait();

    var path = Path.Combine(_dir, "model.envelope.scene_views.parquet");
    if (tiers == 0)
    {
      File.Exists(path).Should().BeFalse();
      return;
    }

    using var db = new DuckDBConnection("Data Source=:memory:");
    db.Open();
    View(db, "scene_views");
    Scalar(db, "SELECT count(*) FROM scene_views WHERE view = 0").Should().Be(tiers);
    Scalar(db, $"SELECT count(*) FROM scene_views WHERE ord = 0 AND source = 'rel' AND ref = '{(int)RelKind.InModel}'")
      .Should()
      .Be(modelAtOrd0);
  }

  // Reference impl of the Navis rule — modelCount = pDb.getModels().Count (the appended federation members).
  private static List<SceneViewKey> NavisDefaultKeys(int modelCount) =>
    modelCount > 1 ? new List<SceneViewKey> { SceneViewKey.Rel(RelKind.InModel) } : new List<SceneViewKey>();

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
