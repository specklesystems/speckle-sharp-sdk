using Speckle.Sdk.Api.GraphQL.Enums;

namespace Speckle.Sdk.Api.GraphQL.Models;

public class Project
{
  public required bool allowPublicComments { get; init; }
  public required DateTime createdAt { get; init; }
  public required string? description { get; init; }
  public required string id { get; init; }
  public required string name { get; init; }
  public required string? role { get; init; }
  public required List<string> sourceApps { get; init; }
  public required DateTime updatedAt { get; init; }
  public required ProjectVisibility visibility { get; init; }
  public required string? workspaceId { get; init; }
}

public sealed class ProjectWithModels : Project
{
  public required ResourceCollection<Model> models { get; init; }
}

public sealed class ProjectWithTeam : Project
{
  public required List<PendingStreamCollaborator> invitedTeam { get; init; }
  public required List<ProjectCollaborator> team { get; init; }
}

public sealed class ProjectWithPermissions : Project
{
  public required ProjectPermissionChecks permissions { get; init; }
}
