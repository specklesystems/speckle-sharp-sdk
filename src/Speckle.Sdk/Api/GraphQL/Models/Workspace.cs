using Speckle.Newtonsoft.Json;

namespace Speckle.Sdk.Api.GraphQL.Models;

public class LimitedWorkspace
{
  public string id { get; init; }
  public string name { get; init; }
  public string? role { get; init; }
  public string slug { get; init; }
  public string? logoUri { get; init; }
  public string? description { get; init; }

  [JsonIgnore]
  [Obsolete($"Deprecated, use {nameof(logoUri)} instead", true)]
  public string? logo { get; init; }
}

public class Workspace : LimitedWorkspace
{
  public DateTime createdAt { get; init; }
  public DateTime updatedAt { get; init; }
  public bool readOnly { get; init; }
  public WorkspacePermissionChecks permissions { get; init; }

  [JsonIgnore]
  [Obsolete("Workspaces no longer have creation state, is always created true", true)]
  public WorkspaceCreationState? creationState { get; init; }
}

[Obsolete("Workspaces no longer have creation state, is always created true")]
public sealed class WorkspaceCreationState
{
  public bool completed { get; init; }
}

public sealed class WorkspacePermissionChecks
{
  public PermissionCheckResult canCreateProject { get; init; }
}
