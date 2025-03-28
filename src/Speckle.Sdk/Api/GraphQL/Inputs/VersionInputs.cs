﻿namespace Speckle.Sdk.Api.GraphQL.Inputs;

public sealed record UpdateVersionInput(string versionId, string projectId, string? message);

public sealed record MoveVersionsInput(string projectId, string targetModelName, IReadOnlyList<string> versionIds);

public sealed record DeleteVersionsInput(IReadOnlyList<string> versionIds, string projectId);

public sealed record CreateVersionInput(
  string objectId,
  string modelId,
  string projectId,
  string? message = null,
  string? sourceApplication = ".net",
  int? totalChildrenCount = null,
  IReadOnlyList<string>? parents = null
);

public sealed record MarkReceivedVersionInput(
  string versionId,
  string projectId,
  string sourceApplication,
  string? message = null
);
