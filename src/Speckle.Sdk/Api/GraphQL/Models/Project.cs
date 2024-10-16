#nullable disable
using Speckle.Sdk.Api.GraphQL.Enums;

namespace Speckle.Sdk.Api.GraphQL.Models;

public sealed class Project
{
  public bool AllowPublicComments { get; init; }
  public ProjectCommentCollection commentThreads { get; init; }
  public DateTime createdAt { get; init; }
  public string description { get; init; }
  public string id { get; init; }
  public List<PendingStreamCollaborator> invitedTeam { get; init; }
  public ResourceCollection<Model> models { get; init; }
  public string name { get; init; }
  public List<FileUpload> pendingImportedModels { get; init; }
  public string role { get; init; }
  public List<string> sourceApps { get; init; }
  public List<ProjectCollaborator> team { get; init; }
  public DateTime updatedAt { get; init; }
  public ProjectVisibility visibility { get; init; }

  public List<ViewerResourceGroup> viewerResources { get; init; }
  public ResourceCollection<Version> versions { get; init; }
  public Model model { get; init; }
  public List<ModelsTreeItem> modelChildrenTree { get; init; }
  public ResourceCollection<ModelsTreeItem> modelsTree { get; init; }

  public string workspaceId { get; init; }
}
