using Version = Speckle.Sdk.Api.GraphQL.Models.Version;

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

/// <param name="versionId"></param>
/// <param name="projectId"></param>
/// <param name="sourceApplication">IMPORTANT: this is meant to be the slug of the application that has done the receiving, not to be confused with <see cref="Version.sourceApplication"/></param>
/// <param name="message"></param>
public record MarkReceivedVersionInput(
  string versionId,
  string projectId,
  string sourceApplication,
  string? message = null
);
