namespace Speckle.Sdk.Api.GraphQL.Models;

public sealed class Workspace
{
  public string id { get; init; }
  public string name { get; init; }
  public string role { get; init; }
  public string slug { get; init; }
  public string? description { get; init; }
  public string? logo { get; init; }
  public DateTime? createdAt { get; init; }
  public DateTime? updatedAt { get; init; }
  public bool? readOnly { get; init; }
  public WorkspacePermissionChecks permissions { get; init; }
  public WorkspaceCreationState? creationState { get; init; }
}

public sealed class WorkspaceCreationState
{
  public bool completed { get; init; }
}

public sealed class WorkspacePermissionChecks
{
  public PermissionCheckResult canCreateProject { get; init; }
}
