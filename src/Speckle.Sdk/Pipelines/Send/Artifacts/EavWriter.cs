#if NET8_0_OR_GREATER
using Parquet.Schema;

namespace Speckle.Sdk.Pipelines.Send.Artifacts;

/// <summary>
/// Speckle 4.0 compact, interned eav writer — now DIRECT Zstd PARQUET (one file per table) instead of
/// DuckDB, à la <see cref="GeometriesParquetWriter"/>: passive columnar files, no WAL/checkpoint/index
/// (so no finalize-OOM cliff), bounded memory at any row count, ~80% smaller than the .duckdb form.
/// <code>
///   {base}.eav.objects.parquet(object_index, application_id)        -- one row per object (the K dictionary)
///   {base}.eav.paths.parquet(path_index, path)                     -- SHARED path vocabulary
///   {base}.eav.eav.parquet(object_index, path_index, value_*)       -- INSTANCE-scoped property rows
///   {base}.eav.types.parquet(type_index, type_key)                 -- type dictionary
///   {base}.eav.type_eav.parquet(type_index, path_index, value_*)    -- TYPE-scoped params, once per type
///   {base}.eav.object_type.parquet(object_index, type_index)       -- the weak ref
///   {base}.eav.manifest.sql                                        -- attaches the above + the flat-read view
/// </code>
/// The <c>object_properties</c> view (instance ∪ type) is shipped as the manifest, run on attach — DuckDB
/// reads the parquet natively. Interning (applicationId/path/type_key → dense int) and the type dedup are
/// unchanged. Not thread-safe: calls are sequential.
/// </summary>
public sealed class EavWriter : IDisposable
{
  public string OutputDir { get; }
  public string BaseName { get; }

  /// <summary>The manifest entry point (kept named <c>EavDbPath</c> for caller compatibility).</summary>
  public string EavDbPath { get; }

  private readonly ParquetTableWriter _objects;
  private readonly ParquetTableWriter _paths;
  private readonly ParquetTableWriter _eav;
  private readonly ParquetTableWriter _types;
  private readonly ParquetTableWriter _typeEav;
  private readonly ParquetTableWriter _objectType;

  // Interning: applicationId / path / type_key → dense sequential int32 id (0..N-1, first-seen order).
  private readonly Dictionary<string, int> _objectIndex = new(StringComparer.Ordinal);
  private readonly Dictionary<string, int> _pathIndex = new(StringComparer.Ordinal);
  private readonly Dictionary<string, int> _typeIndex = new(StringComparer.Ordinal);

  private bool _completed;

  public EavWriter(string outputDir, string baseName)
  {
    Directory.CreateDirectory(outputDir);
    OutputDir = outputDir;
    BaseName = baseName;
    EavDbPath = P("manifest.sql");

    _objects = new ParquetTableWriter(P("objects.parquet"), new ParquetSchema(I("object_index"), S("application_id")));
    _paths = new ParquetTableWriter(P("paths.parquet"), new ParquetSchema(I("path_index"), S("path")));
    _eav = new ParquetTableWriter(P("eav.parquet"), EavSchema("object_index"));
    _types = new ParquetTableWriter(P("types.parquet"), new ParquetSchema(I("type_index"), S("type_key")));
    _typeEav = new ParquetTableWriter(P("type_eav.parquet"), EavSchema("type_index"));
    _objectType = new ParquetTableWriter(
      P("object_type.parquet"),
      new ParquetSchema(I("object_index"), I("type_index"))
    );
  }

  /// <summary>Appends the flattened rows for one object, keyed by its <paramref name="applicationId"/>.</summary>
  public void AddRows(string applicationId, IEnumerable<EavRow> rows)
  {
    EnsureNotCompleted();
    int objectIndex = GetOrAddObject(applicationId);
    foreach (var row in rows)
    {
      _eav.AddRow(objectIndex, GetOrAddPath(row.Path), row.ValueText, row.ValueNum, Boolean(row), row.Units, row.InternalDefinitionName);
    }
  }

  /// <summary>
  /// Interns <paramref name="applicationId"/> to its dense <c>object_index</c>, writing the
  /// <c>objects</c> dictionary row on first sight. Public so the envelope path resolves the SAME K.
  /// </summary>
  public int GetOrAddObject(string applicationId)
  {
    if (_objectIndex.TryGetValue(applicationId, out var idx))
    {
      return idx;
    }
    idx = _objectIndex.Count;
    _objectIndex.Add(applicationId, idx);
    _objects.AddRow(idx, applicationId);
    return idx;
  }

  /// <summary>
  /// Links <paramref name="applicationId"/> to its type (<paramref name="typeKey"/>) via
  /// <c>object_type</c>, and writes that type's parameters to <c>type_eav</c> ONCE (deduped).
  /// <paramref name="typeRowsFactory"/> is invoked only on the type's first sight.
  /// </summary>
  public void AddType(string applicationId, string typeKey, Func<IEnumerable<EavRow>> typeRowsFactory)
  {
    EnsureNotCompleted();

    int typeIndex = GetOrAddType(typeKey, out var isNew);
    if (isNew)
    {
      foreach (var row in typeRowsFactory())
      {
        _typeEav.AddRow(typeIndex, GetOrAddPath(row.Path), row.ValueText, row.ValueNum, Boolean(row), row.Units, row.InternalDefinitionName);
      }
    }
    _objectType.AddRow(GetOrAddObject(applicationId), typeIndex);
  }

  /// <summary>Flushes all parquet tables and writes the attach/view manifest.</summary>
  public void Complete()
  {
    if (_completed)
    {
      return;
    }
    _completed = true;

    _objects.Complete();
    _paths.Complete();
    _eav.Complete();
    _types.Complete();
    _typeEav.Complete();
    _objectType.Complete();

    File.WriteAllText(EavDbPath, Manifest());
  }

  public void Dispose()
  {
    if (_completed)
    {
      return;
    }
    _completed = true;
    SafeDispose(_objects);
    SafeDispose(_paths);
    SafeDispose(_eav);
    SafeDispose(_types);
    SafeDispose(_typeEav);
    SafeDispose(_objectType);
  }

  private int GetOrAddPath(string path)
  {
    if (_pathIndex.TryGetValue(path, out var idx))
    {
      return idx;
    }
    idx = _pathIndex.Count;
    _pathIndex.Add(path, idx);
    _paths.AddRow(idx, path);
    return idx;
  }

  private int GetOrAddType(string typeKey, out bool isNew)
  {
    if (_typeIndex.TryGetValue(typeKey, out var idx))
    {
      isNew = false;
      return idx;
    }
    idx = _typeIndex.Count;
    _typeIndex.Add(typeKey, idx);
    _types.AddRow(idx, typeKey);
    isNew = true;
    return idx;
  }

  private static bool? Boolean(EavRow row) =>
    row.Type == "boolean" && bool.TryParse(row.ValueText, out var b) ? b : null;

  private string P(string suffix) => System.IO.Path.Combine(OutputDir, $"{BaseName}.eav.{suffix}");

  // Manifest: attach each parquet as a view + the flat-read `object_properties` (instance ∪ type).
  // Filenames are relative so the consumer runs it from the artefact directory.
  private string Manifest() =>
    $@"-- Speckle 4.0 eav artefact. Run from the artefact directory (DuckDB reads parquet natively).
CREATE VIEW objects      AS SELECT * FROM read_parquet('{BaseName}.eav.objects.parquet');
CREATE VIEW paths        AS SELECT * FROM read_parquet('{BaseName}.eav.paths.parquet');
CREATE VIEW eav          AS SELECT * FROM read_parquet('{BaseName}.eav.eav.parquet');
CREATE VIEW types        AS SELECT * FROM read_parquet('{BaseName}.eav.types.parquet');
CREATE VIEW type_eav     AS SELECT * FROM read_parquet('{BaseName}.eav.type_eav.parquet');
CREATE VIEW object_type  AS SELECT * FROM read_parquet('{BaseName}.eav.object_type.parquet');
CREATE VIEW object_properties AS
  SELECT object_index, path_index, value_string, value_double, value_boolean, unit, internal_definition_name
    FROM eav
  UNION ALL
  SELECT ot.object_index, te.path_index, te.value_string, te.value_double, te.value_boolean, te.unit, te.internal_definition_name
    FROM object_type ot JOIN type_eav te ON te.type_index = ot.type_index;
";

  private void EnsureNotCompleted()
  {
    if (_completed)
    {
      throw new InvalidOperationException("Writer already completed.");
    }
  }

  // eav / type_eav share the same value-row shape (keyed by object_index or type_index).
  private static ParquetSchema EavSchema(string keyColumn) =>
    new(
      I(keyColumn),
      I("path_index"),
      S("value_string"),
      new DataField<double?>("value_double"),
      new DataField<bool?>("value_boolean"),
      S("unit"),
      S("internal_definition_name")
    );

  private static DataField I(string name) => new DataField<int>(name);

  private static DataField S(string name) => new DataField<string>(name);

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
#endif
