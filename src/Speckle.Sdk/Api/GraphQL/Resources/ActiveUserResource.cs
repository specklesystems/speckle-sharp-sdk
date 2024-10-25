using GraphQL;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Models.Responses;

namespace Speckle.Sdk.Api.GraphQL.Resources;

public sealed class ActiveUserResource
{
  private readonly ISpeckleGraphQLClient _client;

  internal ActiveUserResource(ISpeckleGraphQLClient client)
  {
    _client = client;
  }

  /// <summary>
  /// Gets the currently active user profile (as extracted from the authorization header)
  /// </summary>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <returns>the requested user, or null if <see cref="Client"/> was initialised with an unauthenticated account</returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task<User?> Get(CancellationToken cancellationToken = default)
  {
    //language=graphql
    const string QUERY = """
       query User {
        data:activeUser {
          id
          email
          name
          bio
          company
          avatar
          verified
          role
        }
      }
      """;
    var request = new GraphQLRequest { Query = QUERY };

    var response = await _client
      .ExecuteGraphQLRequest<NullableResponse<User?>>(request, cancellationToken)
      .ConfigureAwait(false);

    return response.data;
  }

  /// <param name="input"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  public async Task<User> Update(UserUpdateInput input, CancellationToken cancellationToken = default)
  {
    //todo:test
    //language=graphql
    const string QUERY = """
      mutation ActiveUserMutations($input: UserUpdateInput!) {
        data:activeUserMutations {
          data:update(user: $input) {
            id
            email
            name
            bio
            company
            avatar
            verified
            role
          }
        }
      }
      """;
    var request = new GraphQLRequest { Query = QUERY, Variables = new { input } };

    var response = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<User>>>(request, cancellationToken)
      .ConfigureAwait(false);

    return response.data.data;
  }

  /// <param name="limit">Max number of projects to fetch</param>
  /// <param name="cursor">Optional cursor for pagination</param>
  /// <param name="filter">Optional filter</param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task<ResourceCollection<Project>> GetProjects(
    int limit = ServerLimits.DEFAULT_PAGINATION_REQUEST,
    string? cursor = null,
    UserProjectsFilter? filter = null,
    CancellationToken cancellationToken = default
  )
  {
    //language=graphql
    const string QUERY = """
       query User($limit : Int!, $cursor: String, $filter: UserProjectsFilter) {
        data:activeUser {
          data:projects(limit: $limit, cursor: $cursor, filter: $filter) {
             totalCount
             cursor
             items {
                id
                name
                description
                visibility
                allowPublicComments
                role
                createdAt
                updatedAt
                sourceApps
                workspaceId
             }
          }
        }
      }
      """;
    var request = new GraphQLRequest
    {
      Query = QUERY,
      Variables = new
      {
        limit,
        cursor,
        filter,
      },
    };

    var response = await _client
      .ExecuteGraphQLRequest<NullableResponse<RequiredResponse<ResourceCollection<Project>>?>>(
        request,
        cancellationToken
      )
      .ConfigureAwait(false);

    if (response.data is null)
    {
      throw new SpeckleGraphQLException("GraphQL response indicated that the ActiveUser could not be found");
    }

    return response.data.data;
  }

  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task<List<PendingStreamCollaborator>> GetProjectInvites(CancellationToken cancellationToken = default)
  {
    //language=graphql
    const string QUERY = """
      query ProjectInvites {
        data:activeUser {
          data:projectInvites {
            id
            inviteId
            invitedBy {
              avatar
              bio
              company
              id
              name
              role
              verified
            }
            projectId
            projectName
            role
            title
            token
            user {
              id
              name
              bio
              company
              verified
              avatar
              role
            }
          }
        }
      }
      """;

    var request = new GraphQLRequest { Query = QUERY };

    var response = await _client
      .ExecuteGraphQLRequest<NullableResponse<RequiredResponse<List<PendingStreamCollaborator>>?>>(
        request,
        cancellationToken
      )
      .ConfigureAwait(false);

    if (response.data is null)
    {
      throw new SpeckleGraphQLException("GraphQL response indicated that the ActiveUser could not be found");
    }

    return response.data.data;
  }

  /// <inheritdoc cref="GetProjectInvites"/>
  [Obsolete($"Renamed to {nameof(GetProjectInvites)}")]
  public async Task<List<PendingStreamCollaborator>> ProjectInvites(CancellationToken cancellationToken = default) =>
    await GetProjectInvites(cancellationToken).ConfigureAwait(false);
}
