namespace Speckle.Sdk.Api.GraphQL.Inputs;

public record GenerateFileUploadUrlInput(string projectId, string fileName);

[Obsolete(FileImportInputBase.FILE_IMPORT_DEPRECATION_MESSAGE)]
public record StartFileImportInput(string projectId, string modelId, string fileId, string etag);

[Obsolete(FileImportInputBase.FILE_IMPORT_DEPRECATION_MESSAGE)]
public record FileImportResult(
  double durationSeconds,
  double downloadDurationSeconds,
  double parseDurationSeconds,
  string parser,
  string? versionId
);

public abstract class FileImportInputBase
{
  internal const string FILE_IMPORT_DEPRECATION_MESSAGE =
    "Part of the old API surface and will be removed in the future. Use the new ingestion API instead. Field will be deleted on June 1st, 2026";

  [Obsolete(FileImportInputBase.FILE_IMPORT_DEPRECATION_MESSAGE)]
  protected FileImportInputBase() { }

  public required string projectId { get; init; }
  public required string jobId { get; init; }
  public required IReadOnlyCollection<string> warnings { get; init; }

  [Obsolete(FileImportInputBase.FILE_IMPORT_DEPRECATION_MESSAGE)]
  public required FileImportResult result { get; init; }
}

#pragma warning disable CA1822 //Mark members as static

[Obsolete(FILE_IMPORT_DEPRECATION_MESSAGE)]
public sealed class FileImportSuccessInput() : FileImportInputBase()
{
  public const string TYPE_STATUS = "success";

  public string status => TYPE_STATUS;
}

[Obsolete(FILE_IMPORT_DEPRECATION_MESSAGE)]
public sealed class FileImportErrorInput() : FileImportInputBase()
{
  public const string TYPE_STATUS = "error";

  public string status => TYPE_STATUS;
  public required string reason { get; init; }
}
