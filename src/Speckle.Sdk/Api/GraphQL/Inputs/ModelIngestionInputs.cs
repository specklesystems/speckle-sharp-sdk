using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Api.GraphQL.Enums;

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

public record ModelIngestionStartProcessingInput(
  string ingestionId,
  string projectId,
  string progressMessage,
  SourceDataInput sourceData
);

public record ModelIngestionRequeueInput(string ingestionId, string projectId, string progressMessage);

public record ProjectModelIngestionSubscriptionInput(
  string projectId,
  ModelIngestionReference ingestionReference,
  [property: JsonIgnore] ProjectModelIngestionUpdatedMessageType messageType
)
{
  // The Newtonsoft serializer is setup to handle SCREAMING_CASE enums.
  // But the API requires the enum to look exactly like they are
  [JsonProperty(nameof(messageType))]
  public string serializedType => messageType.ToString();
}

/// <remarks>
/// <c>@oneOf</c> i.e. server expects <b>either</b> <paramref name="ingestionId"/> or <paramref name="modelId"/>, but not both.
/// </remarks>
/// <param name="ingestionId"></param>
/// <param name="modelId"></param>
public record ModelIngestionReference(string? ingestionId, string? modelId);
