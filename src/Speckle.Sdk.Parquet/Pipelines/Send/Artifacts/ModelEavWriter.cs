using Speckle.Bundle.Spec;

namespace Speckle.Sdk.Pipelines.Send.Artifacts;

/// <summary>
/// Writer for the OPTIONAL <c>{base}.eav.model.parquet</c> — MODEL/document-scoped attributes (Revit project
/// information / reference-point transform, Civil3D drawing settings, Grasshopper document facts): facts with
/// no owning object, so they cannot ride <c>eav.eav</c>. Object-less eav rows:
/// <code>
///   {base}.eav.model.parquet(path, value_string, value_double, value_boolean, unit)
/// </code>
/// Exactly one value column is set per row (consumer coalesces); <c>path</c> is inlined rather than interned
/// via <c>eav.paths</c> — the table is tiny and stays self-contained. Lazily constructed by the pipeline so a
/// bundle with no model rows ships NO file (feature-detected by presence). Not thread-safe: calls are sequential.
/// </summary>
public sealed class ModelEavWriter : IDisposable
{
  private readonly ParquetTableWriter _rows;
  private bool _completed;

  public ModelEavWriter(string outputDir, string baseName, ParquetWriteScheduler scheduler)
  {
    Directory.CreateDirectory(outputDir);
    _rows = new ParquetTableWriter(
      System.IO.Path.Combine(outputDir, $"{baseName}.eav.model.parquet"),
      BundleSchemas.Model,
      BundleCols.Model.ColumnCount,
      scheduler
    );
  }

  /// <summary>Appends one model-scoped attribute row. Exactly one of <paramref name="valueString"/> /
  /// <paramref name="valueDouble"/> / <paramref name="valueBoolean"/> must be set.</summary>
  public void AddRow(string path, string? valueString, double? valueDouble, bool? valueBoolean, string? unit)
  {
    if (_completed)
    {
      throw new InvalidOperationException("Writer already completed.");
    }
    _rows.AddRow(path, valueString, valueDouble, valueBoolean, unit);
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
