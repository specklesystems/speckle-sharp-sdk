namespace Speckle.Sdk.Api.GraphQL.Inputs;

public record SourceDataInput(
  string sourceApplicationSlug,
  string sourceApplicationVersion,
  string? fileName,
  long? fileSizeBytes
);

public record ModelIngestionCreateInput(
  string modelId,
  string projectId,
  string progressMessage,
  SourceDataInput sourceData
);

public record ModelIngestionUpdateInput(string ingestionId, string projectId, string progressMessage, double? progress);

public record ModelIngestionSuccessInput(string ingestionId, string projectId, string rootObjectId);

public record ModelIngestionFailedInput(
  string ingestionId,
  string projectId,
  string errorReason,
  string? errorStacktrace
)
{
  public static ModelIngestionFailedInput FromException(string ingestionId, string projectId, Exception ex)
  {
    return new ModelIngestionFailedInput(ingestionId, projectId, ex.Message, ex.ToString());
  }
}

public record ModelIngestionCancelledInput(string ingestionId, string projectId, string cancellationMessage);
