using Speckle.Newtonsoft.Json;

namespace Speckle.Sdk.Api.GraphQL.Models;

public class LimitedWorkspace
{
  public required string id { get; init; }
  public required string name { get; init; }
  public string? role { get; init; }
  public required string slug { get; init; }
  public string? logoUrl { get; init; }
  public string? description { get; init; }

  [JsonIgnore]
  [Obsolete($"Deprecated, use {nameof(logoUrl)} instead", true)]
  public string? logo { get; init; }
}

public class Workspace : LimitedWorkspace
{
  public required DateTime createdAt { get; init; }
  public required DateTime updatedAt { get; init; }
  public required bool readOnly { get; init; }
  public required WorkspacePermissionChecks permissions { get; init; }

  [JsonIgnore]
  [Obsolete("Workspaces no longer have creation state, is always created true", true)]
  public WorkspaceCreationState? creationState { get; init; }
}

[Obsolete("Workspaces no longer have creation state, is always created true")]
public sealed class WorkspaceCreationState
{
  public required bool completed { get; init; }
}

public sealed class WorkspacePermissionChecks
{
  public required PermissionCheckResult canCreateProject { get; init; }
}
