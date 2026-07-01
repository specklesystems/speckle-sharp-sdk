#if NETSTANDARD2_0 || NET8_0_OR_GREATER
using Parquet.Schema;

namespace Speckle.Sdk.Pipelines.Send.Artifacts;

/// <summary>
/// Speckle 4.0 structural-analysis results writer — direct Zstd Parquet, one table:
/// <code>
///   {base}.eav.structural-results.parquet(
///       object_index, location, result_type, load_case, component, station, step, value, value_text)
/// </code>
/// A per-DOMAIN (not per-connector) long/fact table for structural analysis + design results, shared by
/// ETABS/CSi and TSD (and future SAP/Robot). One row per leaf value.
/// <list type="bullet">
///   <item><b>Object-level</b> results (frame forces, joint reactions, pier/spandrel forces) set
///   <c>object_index</c> — the SAME dense K the object was interned with in <c>eav.objects</c> — so results
///   join straight back to the member/joint.</item>
///   <item><b>Model-level</b> results (story drift/force, modal period, base reaction) leave
///   <c>object_index</c> null and identify themselves via <c>location</c> (story) and/or <c>step</c> (mode).</item>
/// </list>
/// The axes (<c>load_case</c> / <c>station</c> / <c>step</c> / <c>component</c>) are typed columns, NOT baked
/// into a property path — so the eav path dictionary stays tiny and results stay queryable/range-able.
/// <c>value</c> holds numeric results; <c>value_text</c> holds non-numeric design verdicts (e.g. PASS/FAIL).
/// Other domains (environmental, thermal, …) get their own <c>{base}.eav.{domain}-results.parquet</c> when
/// they arrive. Not thread-safe: calls are sequential.
/// </summary>
public sealed class StructuralResultsWriter : IDisposable
{
  public string OutputDir { get; }
  public string BaseName { get; }

  private readonly ParquetTableWriter _results;
  private bool _completed;

  public StructuralResultsWriter(string outputDir, string baseName, ParquetWriteScheduler scheduler)
  {
    Directory.CreateDirectory(outputDir);
    OutputDir = outputDir;
    BaseName = baseName;

    _results = new ParquetTableWriter(
      System.IO.Path.Combine(outputDir, $"{baseName}.eav.structural-results.parquet"),
      new ParquetSchema(
        new DataField<int?>("object_index"),
        S("location"),
        S("result_type"),
        S("load_case"),
        S("component"),
        new DataField<double?>("station"),
        new DataField<int?>("step"),
        new DataField<double?>("value"),
        S("value_text")
      ),
      scheduler
    );
  }

  /// <summary>
  /// Appends one result row. Object-level → pass the object's <paramref name="objectIndex"/> (leave
  /// <paramref name="location"/> null); model-level → pass null <paramref name="objectIndex"/> with a
  /// <paramref name="location"/> and/or <paramref name="step"/>. Numeric results set <paramref name="value"/>;
  /// non-numeric (design verdicts) set <paramref name="valueText"/>.
  /// </summary>
  public void AddRow(
    int? objectIndex,
    string? location,
    string resultType,
    string loadCase,
    string component,
    double? station,
    int? step,
    double? value,
    string? valueText
  )
  {
    if (_completed)
    {
      throw new InvalidOperationException("Writer already completed.");
    }
    _results.AddRow(objectIndex, location, resultType, loadCase, component, station, step, value, valueText);
  }

  public void Complete()
  {
    if (_completed)
    {
      return;
    }
    _completed = true;
    _results.Complete();
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
      _results.Dispose();
    }
#pragma warning disable CA1031 // cleanup path: swallow so the original failure propagates unmasked
    catch (Exception)
#pragma warning restore CA1031
    {
      // Intentionally ignored.
    }
  }

  private static DataField S(string name) => new DataField<string>(name);
}
#endif
