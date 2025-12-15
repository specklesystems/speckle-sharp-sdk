namespace Speckle.Sdk.Api.GraphQL.Models;

public class LimitedWorkspace
{
  public required string id { get; init; }
  public required string name { get; init; }
  public required string? role { get; init; }
  public required string slug { get; init; }
  public required string? logo { get; init; }
  public required string? description { get; init; }
}

public class Workspace : LimitedWorkspace
{
  public required DateTime createdAt { get; init; }
  public required DateTime updatedAt { get; init; }
  public required bool readOnly { get; init; }
  public required WorkspacePermissionChecks permissions { get; init; }
  public required WorkspaceCreationState? creationState { get; init; }
}

public sealed class WorkspaceCreationState
{
  public required bool completed { get; init; }
}

public sealed class WorkspacePermissionChecks
{
  public required PermissionCheckResult canCreateProject { get; init; }
}
