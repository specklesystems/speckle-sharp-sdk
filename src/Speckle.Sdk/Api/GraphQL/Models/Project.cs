using Speckle.Sdk.Api.GraphQL.Enums;

namespace Speckle.Sdk.Api.GraphQL.Models;

public class Project
{
  public bool allowPublicComments { get; init; }
  public DateTime createdAt { get; init; }
  public string? description { get; init; }
  public string id { get; init; }
  public string name { get; init; }
  public string? role { get; init; }
  public List<string> sourceApps { get; init; }
  public DateTime updatedAt { get; init; }
  public ProjectVisibility visibility { get; init; }
  public string? workspaceId { get; init; }
}

public sealed class ProjectWithModels : Project
{
  public ResourceCollection<Model> models { get; init; }
}

public sealed class ProjectWithTeam : Project
{
  public List<PendingStreamCollaborator> invitedTeam { get; init; }
  public List<ProjectCollaborator> team { get; init; }
}
