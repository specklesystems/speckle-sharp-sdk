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
  /// <exception cref="SpeckleException">The ActiveUser could not be found (e.g. the client is not authenticated)</exception>
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
      throw new SpeckleException("GraphQL response indicated that the ActiveUser could not be found");
    }

    return response.data.data;
  }

  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  /// <exception cref="SpeckleException">The ActiveUser could not be found (e.g. the client is not authenticated)</exception>
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
      throw new SpeckleException("GraphQL response indicated that the ActiveUser could not be found");
    }

    return response.data.data;
  }

  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  /// <exception cref="SpeckleException">The ActiveUser could not be found (e.g. the client is not authenticated)</exception>
  public async Task<PermissionCheckResult> CanCreatePersonalProjects(CancellationToken cancellationToken = default)
  {
    //language=graphql
    const string QUERY = """
      query CanCreatePersonalProject {
        data:activeUser {
          data:permissions {
            data:canCreatePersonalProject {
              authorized
              code
              message
            }
          }
        }
      }
      """;

    var request = new GraphQLRequest { Query = QUERY };

    var response = await _client
      .ExecuteGraphQLRequest<NullableResponse<RequiredResponse<RequiredResponse<PermissionCheckResult>>?>>(
        request,
        cancellationToken
      )
      .ConfigureAwait(false);

    if (response.data is null)
    {
      throw new SpeckleException("GraphQL response indicated that the ActiveUser could not be found");
    }

    return response.data.data.data;
  }

  /// <remarks>This feature is only available on Workspace enabled servers (e.g. app.speckle.systems)</remarks>
  /// <param name="limit"></param>
  /// <param name="cursor"></param>
  /// <param name="filter"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  /// <exception cref="SpeckleException">The ActiveUser could not be found (e.g. the client is not authenticated)</exception>
  public async Task<ResourceCollection<Workspace>> GetWorkspaces(
    int limit = ServerLimits.DEFAULT_PAGINATION_REQUEST,
    string? cursor = null,
    UserWorkspacesFilter? filter = null,
    CancellationToken cancellationToken = default
  )
  {
    //language=graphql
    const string QUERY = """
      query ActiveUser($limit: Int!, $cursor: String, $filter: UserWorkspacesFilter) {
        data:activeUser {
          data:workspaces(limit: $limit, cursor: $cursor, filter: $filter) {
            cursor
            items {
              id
              name
              role
              slug
              logo
              createdAt
              updatedAt
              readOnly
              description
              creationState
              {
                completed
              }
              permissions {
                canCreateProject {
                  authorized
                  code
                  message
                }
              }
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
      .ExecuteGraphQLRequest<NullableResponse<RequiredResponse<ResourceCollection<Workspace>>?>>(
        request,
        cancellationToken
      )
      .ConfigureAwait(false);

    if (response.data is null)
    {
      throw new SpeckleException("GraphQL response indicated that the ActiveUser could not be found");
    }

    return response.data.data;
  }

  /// <param name="cancellationToken"></param>
  /// <returns>The active (last selected) workspace</returns>
  /// <remarks>note this returns a <see cref="LimitedWorkspace"/>, because it may be a workspace the user is not a member of</remarks>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  /// <exception cref="SpeckleException">The ActiveUser could not be found (e.g. the client is not authenticated)</exception>
  public async Task<LimitedWorkspace?> GetActiveWorkspace(CancellationToken cancellationToken = default)
  {
    //language=graphql
    const string QUERY = """
      query ActiveUser {
        data:activeUser {
          data:activeWorkspace {
            id
            name
            role
            slug
            logo
            description
          }
        }
      }
      """;

    var request = new GraphQLRequest { Query = QUERY };

    var response = await _client
      .ExecuteGraphQLRequest<NullableResponse<NullableResponse<LimitedWorkspace?>?>>(request, cancellationToken)
      .ConfigureAwait(false);

    if (response.data is null)
    {
      throw new SpeckleException("GraphQL response indicated that the ActiveUser could not be found");
    }

    return response.data.data;
  }

  /// <param name="limit">Max number of projects to fetch</param>
  /// <param name="cursor">Optional cursor for pagination</param>
  /// <param name="filter">Optional filter</param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  /// <exception cref="SpeckleException">The ActiveUser could not be found (e.g. the client is not authenticated)</exception>
  public async Task<ResourceCollection<ProjectWithPermissions>> GetProjectsWithPermissions(
    int limit = ServerLimits.DEFAULT_PAGINATION_REQUEST,
    string? cursor = null,
    UserProjectsFilter? filter = null,
    CancellationToken cancellationToken = default
  )
  {
    //language=graphql
    const string QUERY = """
      query User($limit: Int!, $cursor: String, $filter: UserProjectsFilter) {
        data: activeUser {
          data: projects(limit: $limit, cursor: $cursor, filter: $filter) {
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
              permissions {
                canCreateModel {
                  code
                  authorized
                  message
                }
                canDelete {
                  code
                  authorized
                  message
                }
                canLoad {
                  code
                  authorized
                  message
                }
                canPublish {
                  code
                  authorized
                  message
                }
              }
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
      .ExecuteGraphQLRequest<NullableResponse<RequiredResponse<ResourceCollection<ProjectWithPermissions>>?>>(
        request,
        cancellationToken
      )
      .ConfigureAwait(false);

    if (response.data is null)
    {
      throw new SpeckleException("GraphQL response indicated that the ActiveUser could not be found");
    }

    return response.data.data;
  }
}
