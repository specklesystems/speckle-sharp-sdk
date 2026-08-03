using System.Globalization;
using Parquet.Schema;
using SpecArrow = Speckle.Bundle.Spec.ArrowType;
using SpecBundle = Speckle.Bundle.Spec.BundleSpec;
using SpecCatalog = Speckle.Bundle.Spec.Catalog;
using SpecColumn = Speckle.Bundle.Spec.ColumnSpec;
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
///   {base}.envelope.{meta,rel_types,node_kinds}.parquet        -- self-describing catalog (SOT §6)
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

  // ENG-8947: reference-point provision recorded in meta when a producer re-based geometry by a source
  // reference point. kind ∈ projectBasePoint | surveyPoint | internalOriginFallback; offset "x,y,z" in
  // display units (the vector subtracted). Both null = internal origin (no re-basing). See SetReferencePoint.
  private string? _referencePointKind;
  private string? _referencePointOffset;

  // meta.produced_by: the producing host-app slug (e.g. "rhino", "revit") — NOT a writer class name; consumers
  // (e.g. the SketchUp receiver) branch on it to detect same-app round-trips. Null = un-migrated producer fallback.
  private readonly string? _producedBy;

  public EnvelopeWriter(string outputDir, string baseName, ParquetWriteScheduler scheduler, string? producedBy = null)
  {
    Directory.CreateDirectory(outputDir);
    OutputDir = outputDir;
    BaseName = baseName;
    _scheduler = scheduler;
    _producedBy = producedBy;

    _relations = new ParquetTableWriter(P("relations.parquet"), SchemaOf(SpecSchemas.Relations), scheduler);
    _nodes = new ParquetTableWriter(P("nodes.parquet"), SchemaOf(SpecSchemas.Nodes), scheduler);

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
    double? elevation
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
      elevation
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
  /// ENG-8947: records the applied reference-point re-basing in <c>meta</c> (written at <see cref="Complete"/>).
  /// <paramref name="kind"/> ∈ <c>projectBasePoint | surveyPoint | internalOriginFallback</c> (null = internal
  /// origin); <paramref name="offset"/> is <c>"x,y,z"</c> in display units — the vector subtracted from world-space
  /// output (null when not a translation re-basing). Call before <see cref="Complete"/>.
  /// </summary>
  public void SetReferencePoint(string? kind, string? offset)
  {
    _referencePointKind = kind;
    _referencePointOffset = offset;
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

  // meta (SOT §6): schema version + producer + the ENG-8947 reference-point provision. Written at Complete()
  // (not eagerly in the ctor) so producers can record an applied reference-point re-basing via SetReferencePoint
  // first. reference_point_kind/_offset are null unless set → NULL columns (readers that ignore them are unaffected).
  private void WriteMeta()
  {
    using var meta = new ParquetTableWriter(
      P("meta.parquet"),
      new ParquetSchema(I("schema_version"), S("produced_by"), S("reference_point_kind"), S("reference_point_offset")),
      _scheduler
    );
    meta.AddRow(
      SpecBundle.SchemaVersion,
      _producedBy ?? "Speckle.Sdk EnvelopeWriter",
      _referencePointKind,
      _referencePointOffset
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
    using var cv = new ParquetTableWriter(P("camera_views.parquet"), SchemaOf(SpecSchemas.CameraViews), _scheduler);
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

  // Build a Parquet.Net schema from the generated spec column descriptors (the single source of truth
  // for table shapes). DDL nullability → DataField<T> (required) vs DataField<T?> (nullable).
  private static ParquetSchema SchemaOf(SpecColumn[] cols)
  {
    var fields = new Field[cols.Length];
    for (var i = 0; i < cols.Length; i++)
    {
      fields[i] = ToField(cols[i]);
    }
    return new ParquetSchema(fields);
  }

  private static DataField ToField(SpecColumn c) =>
    c.Type switch
    {
      SpecArrow.Int32 => c.Nullable ? new DataField<int?>(c.Name) : new DataField<int>(c.Name),
      SpecArrow.Int64 => c.Nullable ? new DataField<long?>(c.Name) : new DataField<long>(c.Name),
      SpecArrow.Float64 => c.Nullable ? new DataField<double?>(c.Name) : new DataField<double>(c.Name),
      SpecArrow.Boolean => c.Nullable ? new DataField<bool?>(c.Name) : new DataField<bool>(c.Name),
      SpecArrow.Utf8 => new DataField<string>(c.Name),
      SpecArrow.Binary => new DataField<byte[]>(c.Name),
      _ => throw new NotSupportedException($"Unmapped ArrowType {c.Type} for column {c.Name}"),
    };

  private static DataField I(string name) => new DataField<int>(name);

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
