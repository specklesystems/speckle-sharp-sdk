﻿namespace Speckle.Sdk.Api.GraphQL.Inputs;

public sealed record CreateCommentInput(
  CommentContentInput content,
  string projectId,
  string resourceIdString,
  string? screenshot,
  object? viewerState
);

public sealed record EditCommentInput(CommentContentInput content, string commentId);

public sealed record CreateCommentReplyInput(CommentContentInput content, string threadId);

public sealed record CommentContentInput(IReadOnlyCollection<string>? blobIds, object? doc);
