using Speckle.Newtonsoft.Json;

namespace Speckle.Sdk.Api.GraphQL.Models;

public sealed class Comment
{
  public required bool archived { get; init; }
  public required LimitedUser author { get; init; }
  public required string authorId { get; init; }
  public required DateTime createdAt { get; init; }
  public required bool hasParent { get; init; }
  public required string id { get; init; }
  public required Comment? parent { get; init; }
  public required string rawText { get; init; }
  public required ResourceCollection<Comment> replies { get; init; }
  public required CommentReplyAuthorCollection replyAuthors { get; init; }
  public required List<ResourceIdentifier> resources { get; init; } //todo: add resourceIds/baseResourceIds
  public required string? screenshot { get; init; }
  public required DateTime updatedAt { get; init; }
  public required DateTime? viewedAt { get; init; }
  public required List<ViewerResourceItem> viewerResources { get; init; }
  public required SerializedViewerState viewerState { get; init; }
}

/// <summary>
/// See <c>SerializedViewerState</c> in <a href="https://github.com/specklesystems/speckle-server/blob/main/packages/shared/src/viewer/helpers/state.ts">/shared/src/viewer/helpers/state.ts</a>
/// </summary>
/// <remarks>
/// Note, there are many FE/Viewer specific properties on this object that are not reflected here (hence the <see cref="MissingMemberHandling"/> override)
/// We can add them as needed, keeping in mind flexiblity for breaking changes (these classes are intentionally not documented in our schema!)
/// </remarks>
[JsonObject(MissingMemberHandling = MissingMemberHandling.Ignore)]
public sealed class SerializedViewerState
{
  public required ViewerStateUI ui { get; init; }
}

[JsonObject(MissingMemberHandling = MissingMemberHandling.Ignore)]
public sealed class ViewerStateUI
{
  public required ViewerStateCamera camera { get; init; }
}

[JsonObject(MissingMemberHandling = MissingMemberHandling.Ignore)]
public sealed class ViewerStateCamera
{
  public required List<double> position { get; init; }
  public required List<double> target { get; init; }
  public required bool isOrthoProjection { get; init; }
  public required double zoom { get; init; }
}
