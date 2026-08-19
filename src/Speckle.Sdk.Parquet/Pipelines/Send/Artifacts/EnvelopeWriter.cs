using System.Globalization;
using Parquet.Schema;
using SpecBundle = Speckle.Bundle.Spec.BundleSpec;
using SpecCatalog = Speckle.Bundle.Spec.Catalog;
using SpecCols = Speckle.Bundle.Spec.BundleCols;
using SpecSchemas = Speckle.Bundle.Spec.BundleSchemas;

namespace Speckle.Sdk.Pipelines.Send.Artifacts;

/// <summary>One ordered key of a <see cref="SceneView"/> projection. Build via <see cref="Rel"/> / <see cref="Eav"/>
/// so <see cref="Ref"/> is encoded correctly (a rel code as int-string, or a bare eav attr key).</summary>
public sealed record SceneViewKey(ProjectionSource Source, string Ref)
{
  /// <summary>A relation-walk key, e.g. <c>SceneViewKey.Rel(RelKind.OnLevel)</c>.</summary>
  public static SceneViewKey Rel(byte rel) =>
    new(ProjectionSource.Rel, ((int)rel).ToString(CultureInfo.InvariantCulture));

  /// <summary>An eav group-by key, e.g. <c>SceneViewKey.Eav("category")</c>.</summary>
  public static SceneViewKey Eav(string attrKey) => new(ProjectionSource.Eav, attrKey);
}

/// <summary>A producer-authored scene-explorer projection (SOT §8): an ordered list of grouping keys the
/// consumer seeds the model-tree grouping from. Exactly one view per artefact should be
/// <see cref="IsDefault"/>; <see cref="Keys"/> are outermost-first. Producers OMIT keys with no data
/// (conditional keys, e.g. IN_MODEL for a single-model file) and consumers also skip single/empty-group
/// keys, so default ≠ only — users can re-pivot or pick a named alternate.</summary>
public sealed record SceneView(int View, string Name, bool IsDefault, IReadOnlyList<SceneViewKey> Keys);

/// <summary>A named camera viewpoint authored in the source model (Rhino named view, Revit 3D view, SketchUp
/// scene) — NOT a <see cref="SceneView"/> (which is the explorer grouping). Positions/target/ortho params are
/// in <paramref name="Units"/> (model units — the consumer scales like geometry); forward/up are unitless UNIT
/// vectors. <paramref name="Fov"/> is the VERTICAL field of view in DEGREES (perspective only). At most one
/// view per artefact should be <paramref name="IsDefault"/>. Positional (constructed, not object-initialized)
/// so net48 consumers of the ILRepack'd netstandard2.0 assembly never touch init-only setters (CS0570).</summary>
public sealed record CameraView(
  int View,
  string? Name,
  bool IsDefault,
  int? Ord,
  double PosX,
  double PosY,
  double PosZ,
  double ForwardX,
  double ForwardY,
  double ForwardZ,
  double UpX,
  double UpY,
  double UpZ,
  double? TargetX = null,
  double? TargetY = null,
  double? TargetZ = null,
  string? Units = null,
  bool IsOrtho = false,
  double? Fov = null,
  double? LensMm = null,
  double? OrthoHeight = null,
  double? Aspect = null,
  double? Near = null,
  double? Far = null
);

/// <summary>
/// Writes the Speckle 4.0 envelope topology artefact as DIRECT Zstd PARQUET (one file per table). The
/// table SHAPES and the self-describing catalog (<c>rel_types</c>/<c>node_kinds</c>/<c>meta</c>) come from
/// the generated <c>speckle-bundle-spec</c> — this writer no longer hand-declares them.
/// <code>
///   {base}.envelope.relations.parquet(rel, src, dst, ord)      -- typed edges; src/dst namespace = rel
///   {base}.envelope.nodes.parquet(id, kind, name, def_ref,     -- shared value-entities; `subtype` is the
///         transform, units, subtype, argb, opacity,                CONTAINER discriminator (Model/Collection/…)
///         metalness, roughness, emissive, ior, elevation)
///   {base}.envelope.{rel_types,node_kinds}.parquet             -- self-describing catalog (SOT §6)
///   {base}.envelope.meta.parquet(schema_version,               -- catalog + provenance: who produced the
///         produced_by, producer_version, sdk_name,                bundle, with which SDK, and when
///         sdk_version, migrated_from_schema_version)              migrated, from which graph vintage
///   {base}.envelope.scene_views.parquet(view, name,           -- producer-authored grouping projections
///         is_default, ord, source, ref)                          (SOT §8); absent if none
///   {base}.envelope.camera_views.parquet(view, name,          -- named camera viewpoints
///         is_default, ord, pos_*, forward_*, up_*, target_*,     (spec `camera_views`); absent if none
///         units, is_ortho, fov, lens_mm, ortho_height, aspect, near, far)
/// </code>
/// <c>transform</c> is 16 row-major doubles, comma-separated. Not thread-safe: calls are sequential.
/// </summary>
public sealed class EnvelopeWriter : IDisposable
{
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
  private readonly ParquetWriteScheduler _scheduler;
  private readonly List<SceneView> _sceneViews = new();
  private readonly List<CameraView> _cameraViews = new();
  private bool _completed;

  private string? _producedBy;
  private string? _producerVersion;
  private string? _sdkName;
  private string? _sdkVersion;
  private int? _migratedFromSchemaVersion;

  public EnvelopeWriter(string outputDir, string baseName, ParquetWriteScheduler scheduler)
  {
    Directory.CreateDirectory(outputDir);
    OutputDir = outputDir;
    BaseName = baseName;
    _scheduler = scheduler;

    // Spec-schema'd tables carry their generated BundleCols ColumnCount so a spec bump that outruns
    // this writer (or a stale sibling spec checkout) fails at construction / first AddRow — never by
    // silently writing misaligned rows (the Jul 29 empty-nodes incident mode).
    _relations = new ParquetTableWriter(
      P("relations.parquet"),
      SpecSchemas.Relations,
      SpecCols.Relations.ColumnCount,
      scheduler
    );
    _nodes = new ParquetTableWriter(P("nodes.parquet"), SpecSchemas.Nodes, SpecCols.Nodes.ColumnCount, scheduler);

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
  /// non-null (see <see cref="NodeKind"/>). <paramref name="subtype"/> is the CONTAINER discriminator
  /// (e.g. "Model", "Network") — null for non-container kinds.</summary>
  public void AddNode(
    int id,
    byte kind,
    string? name,
    int? defRef,
    string? transform,
    string? units,
    string? subtype,
    int? argb,
    double? opacity,
    double? metalness,
    double? roughness,
    int? emissive,
    double? ior,
    double? elevation,
    string? ghTopology = null
  )
  {
    EnsureNotCompleted();
    _nodes.AddRow(
      id,
      (int)kind,
      name,
      defRef,
      transform,
      units,
      subtype,
      argb,
      opacity,
      metalness,
      roughness,
      emissive,
      ior,
      elevation,
      ghTopology
    );
  }

  /// <summary>Buffers a producer-authored projection (SOT §8); flushed to <c>scene_views.parquet</c> on
  /// <see cref="Complete"/>. Add none and the table is simply absent (consumer falls back to its own default).</summary>
  public void AddSceneView(SceneView view)
  {
    EnsureNotCompleted();
    _sceneViews.Add(view);
  }

  /// <summary>Buffers a named camera viewpoint; flushed to <c>camera_views.parquet</c> on
  /// <see cref="Complete"/>. Add none and the table is simply absent (the model ships no viewpoints).</summary>
  public void AddCameraView(CameraView view)
  {
    EnsureNotCompleted();
    _cameraViews.Add(view);
  }

  /// <summary>
  /// Records the producer information of this bundle in the <c>meta</c> file.
  /// </summary>
  /// <param name="producedBy">slug of the producer of this version</param>
  /// <param name="producerVersion">version of the producer</param>
  /// <param name="sdkName">name of the SDK the producer authored this version with</param>
  /// <param name="sdkVersion">version of that SDK</param>
  /// <param name="migratedFromSchemaVersion">The original schema version for model versions migrated from older schema version. <see langword="null"/> for native authored versions</param>
  /// <remarks>Required before <see cref="Complete"/> — every bundle names its producer.</remarks>
  public void SetProducer(
    string producedBy,
    string producerVersion,
    string sdkName,
    string sdkVersion,
    int? migratedFromSchemaVersion = null
  )
  {
    _producedBy = producedBy;
    _producerVersion = producerVersion;
    _sdkName = sdkName;
    _sdkVersion = sdkVersion;
    _migratedFromSchemaVersion = migratedFromSchemaVersion;
  }

  /// <summary>Flushes the parquet tables and writes the attach manifest.</summary>
  public void Complete()
  {
    if (_completed)
    {
      return;
    }
    if (_producedBy is null)
    {
      throw new InvalidOperationException("SetProducer must be called before Complete() — meta names the producer.");
    }
    _completed = true;
    _relations.Complete();
    _nodes.Complete();
    WriteMeta();
    WriteSceneViews();
    WriteCameraViews();
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

  // meta (SOT §6): schema version + producer provenance. Written at Complete() (not eagerly in the ctor) so
  // producers can record their identity via SetProducer first. The reference-point record does NOT live here —
  // it rides eav.model as referencePoint.* rows (the former meta.reference_point_* columns are removed from the
  // spec; the xyz-only offsets lost rotation and meta is the wrong home for model data).
  private void WriteMeta()
  {
    using var meta = new ParquetTableWriter(
      P("meta.parquet"),
      new ParquetSchema(
        I("schema_version"),
        S("produced_by"),
        S("producer_version"),
        S("sdk_name"),
        S("sdk_version"),
        NI("migrated_from_schema_version")
      ),
      _scheduler
    );
    meta.AddRow(
      SpecBundle.SchemaVersion,
      _producedBy,
      _producerVersion,
      _sdkName,
      _sdkVersion,
      _migratedFromSchemaVersion
    );
  }

  // Self-describing catalog (SOT §6): the rel/kind vocabulary, written once from the
  // generated spec catalog (live + reserved rows; retired ids are absent and never reused). Tiny.
  private void WriteCatalog()
  {
    using (
      var rt = new ParquetTableWriter(
        P("rel_types.parquet"),
        new ParquetSchema(I("rel"), S("name"), S("src_ns"), S("dst_ns")),
        _scheduler
      )
    )
    {
      foreach (var r in SpecCatalog.RelTypes)
      {
        rt.AddRow(r.Id, r.Name, r.SrcNs, r.DstNs);
      }
    }
    using var nk = new ParquetTableWriter(P("node_kinds.parquet"), new ParquetSchema(I("kind"), S("name")), _scheduler);
    foreach (var k in SpecCatalog.NodeKinds)
    {
      nk.AddRow(k.Id, k.Name);
    }
  }

  // Producer-authored projections (SOT §8) — written at Complete(), not eagerly like the static catalog,
  // since the producer declares views over the artefact's lifetime. Absent (no file) when none authored.
  private void WriteSceneViews()
  {
    if (_sceneViews.Count == 0)
    {
      return;
    }
    using var sv = new ParquetTableWriter(
      P("scene_views.parquet"),
      new ParquetSchema(I("view"), S("name"), B("is_default"), I("ord"), S("source"), S("ref")),
      _scheduler
    );
    foreach (var v in _sceneViews)
    {
      for (var ord = 0; ord < v.Keys.Count; ord++)
      {
        var key = v.Keys[ord];
        sv.AddRow(v.View, v.Name, v.IsDefault, ord, key.Source == ProjectionSource.Rel ? "rel" : "eav", key.Ref);
      }
    }
  }

  // Named camera viewpoints — buffered like scene views, written at Complete(), absent (no file) when none.
  // Shape comes from the generated spec (`camera_views`), row order mirrors the column order there.
  private void WriteCameraViews()
  {
    if (_cameraViews.Count == 0)
    {
      return;
    }
    using var cv = new ParquetTableWriter(
      P("camera_views.parquet"),
      SpecSchemas.CameraViews,
      SpecCols.CameraViews.ColumnCount,
      _scheduler
    );
    foreach (var v in _cameraViews)
    {
      cv.AddRow(
        v.View,
        v.Name,
        v.IsDefault,
        v.Ord,
        v.PosX,
        v.PosY,
        v.PosZ,
        v.ForwardX,
        v.ForwardY,
        v.ForwardZ,
        v.UpX,
        v.UpY,
        v.UpZ,
        v.TargetX,
        v.TargetY,
        v.TargetZ,
        v.Units,
        v.IsOrtho,
        v.Fov,
        v.LensMm,
        v.OrthoHeight,
        v.Aspect,
        v.Near,
        v.Far
      );
    }
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

  private static DataField NI(string name) => new DataField<int?>(name);

  private static DataField S(string name) => new DataField<string>(name);

  private static DataField B(string name) => new DataField<bool>(name);

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
