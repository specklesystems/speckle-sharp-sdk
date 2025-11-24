
namespace Speckle.Sdk.Api.GraphQL.Inputs;

public record IngestCreateInput(
  string fileName,
  int? maxIdleTimeoutMinutes,
  string modelId,
  string projectId,
  string sourceApplication,
  string sourceApplicationVersion,
  IReadOnlyDictionary<string, object?> sourceFileData);

public record IngestFinishInput(string id, string? message, string objectId, string projectId);

public record IngestErrorInput(string errorReason, string errorStacktrace, string id, string projectId);

public record CancelRequestInput(string id, string projectId);

public record IngestUpdateInput(string id, double? progress, string? progressMessage, string projectId);
