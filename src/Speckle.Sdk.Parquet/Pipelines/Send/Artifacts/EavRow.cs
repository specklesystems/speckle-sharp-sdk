namespace Speckle.Sdk.Pipelines.Send.Artifacts;

/// <summary>
/// One flat property row destined for the eav.duckdb <c>properties</c> table.
/// Mirrors the server's EavRow (packages/shared/src/filtering/eavExtraction.ts).
/// </summary>
public sealed record EavRow(
  string ObjectId,
  string Path,
  string ValueText,
  double? ValueNum,
  string Type, // "string" | "number" | "boolean"
  string? Units,
  string? InternalDefinitionName
);
