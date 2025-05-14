namespace Speckle.Sdk.Api.GraphQL.Inputs;

public record UpdateVersionInput(string versionId, string projectId, string? message);

public record MoveVersionsInput(string projectId, string targetModelName, IReadOnlyList<string> versionIds);

public record DeleteVersionsInput(IReadOnlyList<string> versionIds, string projectId);

public record CreateVersionInput(
  string objectId,
  string modelId,
  string projectId,
  string? message = null,
  string? sourceApplication = ".net",
  int? totalChildrenCount = null,
  IReadOnlyList<string>? parents = null
);

public record MarkReceivedVersionInput(
  string versionId,
  string projectId,
  string sourceApplication,
  string? message = null
);
