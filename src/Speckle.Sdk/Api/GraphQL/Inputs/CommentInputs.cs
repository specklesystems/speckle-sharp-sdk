namespace Speckle.Sdk.Api.GraphQL.Inputs;

internal record CommentContentInput(IReadOnlyCollection<string>? blobIds, object? doc);

internal record CreateCommentInput(
  CommentContentInput content,
  string projectId,
  string resourceIdString,
  string? screenshot,
  object? viewerState
);

internal record EditCommentInput(CommentContentInput content, string commentId, string projectId);

internal record CreateCommentReplyInput(CommentContentInput content, string threadId, string projectId);

public record MarkCommentViewedInput(string commentId, string projectId);

public record ArchiveCommentInput(string commentId, string projectId, bool archived = true);
