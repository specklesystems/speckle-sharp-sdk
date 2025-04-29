using GraphQL;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Models.Responses;

namespace Speckle.Sdk.Api.GraphQL.Resources;

public sealed class WorkspaceResource
{
  private readonly ISpeckleGraphQLClient _client;

  internal WorkspaceResource(ISpeckleGraphQLClient client)
  {
    _client = client;
  }

  /// <param name="workspaceId"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task<Workspace> Get(string workspaceId, CancellationToken cancellationToken = default)
  {
    //language=graphql
    const string QUERY = """
      query WorkspaceGet($workspaceId: String!) {
        data:workspace(id: $workspaceId) {
          id
          name
          role
          slug
          logo
          createdAt
          updatedAt
          readOnly
          description
          permissions {
            canCreateProject {
              authorized
              code
              message
            }
          }
        }
      }
      """;
    var request = new GraphQLRequest { Query = QUERY, Variables = new { workspaceId } };

    var response = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<Workspace>>>(request, cancellationToken)
      .ConfigureAwait(false);

    return response.data.data;
  }

  /// <param name="workspaceId"></param>
  /// <param name="limit">Max number of projects to fetch</param>
  /// <param name="cursor">Optional cursor for pagination</param>
  /// <param name="filter"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  /// <see cref="Get"/>
  public async Task<ResourceCollection<Project>> GetProjects(
    string workspaceId,
    int limit = ServerLimits.DEFAULT_PAGINATION_REQUEST,
    string? cursor = null,
    WorkspaceProjectsFilter? filter = null,
    CancellationToken cancellationToken = default
  )
  {
    //language=graphql
    const string QUERY = """
      query Workspace($workspaceId: String!, $limit: Int!, $cursor: String, $filter: WorkspaceProjectsFilter) {
        data:workspace(id: $workspaceId) {
          data:projects(limit: $limit, cursor: $cursor, filter: $filter) {
            cursor
            items {
              allowPublicComments
              createdAt
              description
              id
              name
              role
              sourceApps
              updatedAt
              visibility
              workspaceId
            }
            totalCount
          }
        }
      }
      """;

    var request = new GraphQLRequest
    {
      Query = QUERY,
      Variables = new
      {
        workspaceId,
        limit,
        cursor,
        filter,
      },
    };

    var response = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<ResourceCollection<Project>>>>(
        request,
        cancellationToken
      )
      .ConfigureAwait(false);

    return response.data.data;
  }
}
