using Speckle.Bundle.Spec;

namespace Speckle.Sdk.Pipelines.Send.Artifacts;

/// <summary>
/// Writer for the OPTIONAL <c>{base}.eav.property_set_definitions.parquet</c> — the SCHEMAS of AEC/Civil3D
/// property sets, one row per (set, field):
/// <code>
///   {base}.eav.property_set_definitions.parquet(set_name, set_key, field_name, field_id,
///       data_type, default_string, default_double, unit, description, applies_to)
/// </code>
/// VALUES stay per-object in <c>eav.eav</c> (path <c>properties.Property Sets.{set}.{field}</c>, with
/// <c>unit</c> + <c>internal_definition_name</c> = field id) and attachment is DERIVED from those value
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
  /// definition's content hash (identity under same-name collisions); <paramref name="fieldId"/> is the
  /// host's stable field id (joins <c>eav.internal_definition_name</c>). At most one of
  /// <paramref name="defaultString"/> / <paramref name="defaultDouble"/> may be set.</summary>
  public void AddRow(
    string setName,
    string setKey,
    string fieldName,
    int? fieldId,
    string? dataType,
    string? defaultString,
    double? defaultDouble,
    string? unit,
    string? description,
    string? appliesTo
  )
  {
    if (_completed)
    {
      throw new InvalidOperationException("Writer already completed.");
    }
    if (defaultString is not null && defaultDouble is not null)
    {
      throw new ArgumentException(
        $"Property-set field '{setName}.{fieldName}': at most one of defaultString/defaultDouble may be set.",
        nameof(defaultDouble)
      );
    }
    _rows.AddRow(setName, setKey, fieldName, fieldId, dataType, defaultString, defaultDouble, unit, description, appliesTo);
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
