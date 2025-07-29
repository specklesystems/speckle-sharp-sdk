namespace Speckle.Sdk.Api.GraphQL.Inputs;

public record GenerateFileUploadUrlInput(string projectId, string fileName);

public record StartFileImportInput(string projectId, string modelId, string fileId, string etag);

public record FileImportResult(
  double durationSeconds,
  double downloadDurationSeconds,
  double parseDurationSeconds,
  string parser,
  string? versionId
);

public abstract class FileImportInputBase
{
  public required string projectId { get; init; }
  public required string jobId { get; init; }
  public required IReadOnlyCollection<string> warnings { get; init; }
  public required FileImportResult result { get; init; }
}

#pragma warning disable CA1822 //Mark members as static

public sealed class FileImportSuccessInput() : FileImportInputBase()
{
  public const string TYPE_STATUS = "success";

  public string status => TYPE_STATUS;
}

public sealed class FileImportErrorInput() : FileImportInputBase()
{
  public const string TYPE_STATUS = "error";

  public string status => TYPE_STATUS;
  public required string reason { get; init; }
}
