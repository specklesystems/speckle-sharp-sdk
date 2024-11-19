using GraphQL;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Models.Responses;

namespace Speckle.Sdk.Api.GraphQL.Resources;

public sealed class CommentResource
{
  private readonly ISpeckleGraphQLClient _client;

  internal CommentResource(ISpeckleGraphQLClient client)
  {
    _client = client;
  }

  /// <param name="commentId"></param>
  /// <param name="projectId"></param>
  /// <param name="repliesLimit">Max number of comment replies to fetch</param>
  /// <param name="repliesCursor">Optional cursor for pagination</param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task<Comment> Get(
    string commentId,
    string projectId,
    int repliesLimit = ServerLimits.DEFAULT_PAGINATION_REQUEST,
    string? repliesCursor = null,
    CancellationToken cancellationToken = default
  )
  {
    //language=graphql
    const string QUERY = """
      query CommentThreads($projectId: String!, $commentId: String!, $repliesLimit: Int, $repliesCursor: String) {
        data:project(id: $projectId) {
          data:comment(id: $commentId) {
            archived
              authorId
              createdAt
              hasParent
              id
              rawText
              replies(limit: $repliesLimit, cursor: $repliesCursor) {
                cursor
                items {
                  archived
                  authorId
                  createdAt
                  hasParent
                  id
                  rawText
                  updatedAt
                  viewedAt
                }
                totalCount
              }
              resources {
                resourceId
                resourceType
              }
              screenshot
              updatedAt
              viewedAt
              viewerResources {
                modelId
                objectId
                versionId
              }
          }
        }
      }
      """;

    GraphQLRequest request =
      new()
      {
        Query = QUERY,
        Variables = new
        {
          commentId,
          projectId,
          repliesLimit,
          repliesCursor,
        },
      };

    var response = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<Comment>>>(request, cancellationToken)
      .ConfigureAwait(false);

    return response.data.data;
  }

  /// <param name="projectId"></param>
  /// <param name="limit">Max number of comments to fetch</param>
  /// <param name="cursor">Optional cursor for pagination</param>
  /// <param name="filter">Optional filter</param>
  /// <param name="repliesLimit">Max number of comment replies to fetch</param>
  /// <param name="repliesCursor">Optional cursor for pagination</param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task<ProjectCommentCollection> GetProjectComments(
    string projectId,
    int limit = ServerLimits.DEFAULT_PAGINATION_REQUEST,
    string? cursor = null,
    ProjectCommentsFilter? filter = null,
    int repliesLimit = ServerLimits.DEFAULT_PAGINATION_REQUEST,
    string? repliesCursor = null,
    CancellationToken cancellationToken = default
  )
  {
    //language=graphql
    const string QUERY = """
      query CommentThreads($projectId: String!, $cursor: String, $limit: Int!, $filter: ProjectCommentsFilter, $repliesLimit: Int, $repliesCursor: String) {
        data:project(id: $projectId) {
          data:commentThreads(cursor: $cursor, limit: $limit, filter: $filter) {
            cursor
            totalArchivedCount
            totalCount
            items {
              archived
              authorId
              createdAt
              hasParent
              id
              rawText
              replies(limit: $repliesLimit, cursor: $repliesCursor) {
                cursor
                items {
                  archived
                  authorId
                  createdAt
                  hasParent
                  id
                  rawText
                  updatedAt
                  viewedAt
                }
                totalCount
              }
              resources {
                resourceId
                resourceType
              }
              screenshot
              updatedAt
              viewedAt
              viewerResources {
                modelId
                objectId
                versionId
              }
            }
          }
        }
      }
      """;

    GraphQLRequest request =
      new()
      {
        Query = QUERY,
        Variables = new
        {
          projectId,
          cursor,
          limit,
          filter,
          repliesLimit,
          repliesCursor,
        },
      };

    var response = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<ProjectCommentCollection>>>(request, cancellationToken)
      .ConfigureAwait(false);

    return response.data.data;
  }

  /// <remarks>
  /// This function only exists here to be able to integration tests the queries.
  /// The process of creating a comment is more complex and javascript specific than we can expose to our SDKs at this time.
  /// </remarks>
  /// <param name="input"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  internal async Task<Comment> Create(CreateCommentInput input, CancellationToken cancellationToken = default)
  {
    //language=graphql
    const string QUERY = """
      mutation Mutation($input: CreateCommentInput!) {
        data:commentMutations {
          data:create(input: $input) {
            archived
            authorId
            createdAt
            hasParent
            id
            rawText
            resources {
              resourceId
              resourceType
            }
            screenshot
            updatedAt
            viewedAt
            viewerResources {
              modelId
              objectId
              versionId
            }
          }
        }
      }
      """;
    GraphQLRequest request = new(QUERY, variables: new { input });
    var res = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<Comment>>>(request, cancellationToken)
      .ConfigureAwait(false);
    return res.data.data;
  }

  /// <remarks><inheritdoc cref="Create"/></remarks>
  /// <param name="input"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  internal async Task<Comment> Edit(EditCommentInput input, CancellationToken cancellationToken = default)
  {
    //language=graphql
    const string QUERY = """
      mutation Mutation($input: EditCommentInput!) {
        data:commentMutations {
          data:edit(input: $input) {
            archived
            authorId
            createdAt
            hasParent
            id
            rawText
            resources {
              resourceId
              resourceType
            }
            screenshot
            updatedAt
            viewedAt
            viewerResources {
              modelId
              objectId
              versionId
            }
          }
        }
      }
      """;
    GraphQLRequest request = new(QUERY, variables: new { input });
    var res = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<Comment>>>(request, cancellationToken)
      .ConfigureAwait(false);
    return res.data.data;
  }

  /// <param name="input"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task Archive(ArchiveCommentInput input, CancellationToken cancellationToken = default)
  {
    //language=graphql
    const string QUERY = """
      mutation Mutation($input: ArchiveCommentInput!) {
        data:commentMutations {
            data:archive(input: $input)
        }
      }
      """;
    GraphQLRequest request = new(QUERY, variables: new { input });
    var res = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<bool>>>(request, cancellationToken)
      .ConfigureAwait(false);

    if (!res.data.data)
    {
      //This should never happen, the server should never return `false` without providing a reason
      throw new InvalidOperationException("GraphQL data did not indicate success, but no GraphQL error was provided");
    }
  }

  /// <param name="input"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task MarkViewed(MarkCommentViewedInput input, CancellationToken cancellationToken = default)
  {
    //language=graphql
    const string QUERY = """
      mutation Mutation($input: MarkCommentViewedInput!) {
        data:commentMutations {
          data:markViewed(input: $input)
        }
      }
      """;
    GraphQLRequest request = new(QUERY, variables: new { input });
    var res = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<bool>>>(request, cancellationToken)
      .ConfigureAwait(false);

    if (!res.data.data)
    {
      //This should never happen, the server should never return `false` without providing a reason
      throw new InvalidOperationException("GraphQL data did not indicate success, but no GraphQL error was provided");
    }
  }

  /// <remarks><inheritdoc cref="Create"/></remarks>
  /// <param name="input"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  internal async Task<Comment> Reply(CreateCommentReplyInput input, CancellationToken cancellationToken = default)
  {
    //language=graphql
    const string QUERY = """
      mutation Mutation($input: CreateCommentReplyInput!) {
        data:commentMutations {
          data:reply(input: $input) {
            archived
            authorId
            createdAt
            hasParent
            id
            rawText
            resources {
              resourceId
              resourceType
            }
            screenshot
            updatedAt
            viewedAt
            viewerResources {
              modelId
              objectId
              versionId
            }
          }
        }
      }
      """;
    GraphQLRequest request = new(QUERY, variables: new { input });
    var res = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<Comment>>>(request, cancellationToken)
      .ConfigureAwait(false);
    return res.data.data;
  }
}
