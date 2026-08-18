using Speckle.Bundle.Spec;

namespace Speckle.Sdk.Pipelines.Send.Artifacts;

/// <summary>
/// Writer for the OPTIONAL <c>{base}.eav.property_set_definitions.parquet</c> — the SCHEMAS of AEC/Civil3D
/// property sets, one row per (set, field):
/// <code>
///   {base}.eav.property_set_definitions.parquet(set_name, set_key, set_description, field_name,
///       field_bucket_id, data_type, default_string, default_double, default_boolean, unit,
///       description, applies_to)
/// </code>
/// ROW ORDER IS FIELD ORDER (the authored palette order — recreate preserves it). VALUES stay per-object in
/// <c>eav.eav</c> (path <c>properties.Property Sets.{set}.{field}</c>, with <c>unit</c> +
/// <c>internal_definition_name</c> = the FieldBucketId) and attachment is DERIVED from those value
/// paths — an object implements a set iff it carries rows under it. Deliberately NOT <c>type_eav</c>
/// (whose rows an object inherits as its own attributes — schema rows there would surface as fake
/// properties in the eav ∪ type_eav read). Replaces the managed carrier pseudo-object. Lazily constructed
/// so a bundle with no definitions ships NO file. Not thread-safe: calls are sequential.
/// </summary>
public sealed class PropertySetDefinitionsWriter : IDisposable
{
  private readonly ParquetTableWriter _rows;
  private bool _completed;

  public PropertySetDefinitionsWriter(string outputDir, string baseName, ParquetWriteScheduler scheduler)
  {
    Directory.CreateDirectory(outputDir);
    _rows = new ParquetTableWriter(
      System.IO.Path.Combine(outputDir, $"{baseName}.eav.property_set_definitions.parquet"),
      BundleSchemas.PropertySetDefinitions,
      BundleCols.PropertySetDefinitions.ColumnCount,
      scheduler
    );
  }

  /// <summary>Appends one field row of one property-set definition. <paramref name="setKey"/> is the
  /// definition's content hash (SET-level identity under same-name collisions);
  /// <paramref name="fieldBucketId"/> is THE rebind join key — the same string the value rows ship in
  /// <c>eav.internal_definition_name</c> (null ⇒ consumers fall back to matching <paramref name="fieldName"/>
  /// against the value path leaf). At most one of <paramref name="defaultString"/> /
  /// <paramref name="defaultDouble"/> / <paramref name="defaultBoolean"/> may be set.</summary>
  public void AddRow(
    string setName,
    string setKey,
    string? setDescription,
    string fieldName,
    string? fieldBucketId,
    string? dataType,
    string? defaultString,
    double? defaultDouble,
    bool? defaultBoolean,
    string? unit,
    string? description,
    string? appliesTo
  )
  {
    if (_completed)
    {
      throw new InvalidOperationException("Writer already completed.");
    }
    int defaults =
      (defaultString is not null ? 1 : 0) + (defaultDouble is not null ? 1 : 0) + (defaultBoolean is not null ? 1 : 0);
    if (defaults > 1)
    {
      throw new ArgumentException(
        $"Property-set field '{setName}.{fieldName}': at most one of defaultString/defaultDouble/defaultBoolean may be set.",
        nameof(defaultDouble)
      );
    }
    _rows.AddRow(
      setName,
      setKey,
      setDescription,
      fieldName,
      fieldBucketId,
      dataType,
      defaultString,
      defaultDouble,
      defaultBoolean,
      unit,
      description,
      appliesTo
    );
  }

  public void Complete()
  {
    if (_completed)
    {
      return;
    }
    _completed = true;
    _rows.Complete();
  }

  public void Dispose()
  {
    if (_completed)
    {
      return;
    }
    _completed = true;
    try
    {
      _rows.Dispose();
    }
#pragma warning disable CA1031 // cleanup path: swallow so the original failure propagates unmasked
    catch (Exception)
#pragma warning restore CA1031
    {
      // Intentionally ignored.
    }
  }
}
