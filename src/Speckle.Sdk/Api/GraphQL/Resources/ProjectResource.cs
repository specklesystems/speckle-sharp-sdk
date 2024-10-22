using GraphQL;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Models.Responses;

namespace Speckle.Sdk.Api.GraphQL.Resources;

public sealed class ProjectResource
{
  private readonly ISpeckleGraphQLClient _client;

  internal ProjectResource(ISpeckleGraphQLClient client)
  {
    _client = client;
  }

  /// <param name="projectId"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  /// <seealso cref="GetWithModels"/>
  /// <seealso cref="GetWithTeam"/>
  public async Task<Project> Get(string projectId, CancellationToken cancellationToken = default)
  {
    //language=graphql
    const string QUERY = """
      query Project($projectId: String!) {
        data:project(id: $projectId) {
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
      }
      """;
    GraphQLRequest request = new() { Query = QUERY, Variables = new { projectId } };

    var response = await _client
      .ExecuteGraphQLRequest<RequiredResponse<Project>>(request, cancellationToken)
      .ConfigureAwait(false);
    return response.data;
  }

  /// <param name="projectId"></param>
  /// <param name="modelsLimit">Max number of models to fetch</param>
  /// <param name="modelsCursor">Optional cursor for pagination</param>
  /// <param name="modelsFilter">Optional models filter</param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  /// <seealso cref="Get"/>
  /// <seealso cref="GetWithTeam"/>
  public async Task<ProjectWithModels> GetWithModels(
    string projectId,
    int modelsLimit = ServerLimits.DEFAULT_PAGINATION_REQUEST,
    string? modelsCursor = null,
    ProjectModelsFilter? modelsFilter = null,
    CancellationToken cancellationToken = default
  )
  {
    //language=graphql
    const string QUERY = """
      query ProjectGetWithModels($projectId: String!, $modelsLimit: Int!, $modelsCursor: String, $modelsFilter: ProjectModelsFilter) {
        data:project(id: $projectId) {
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
          models(limit: $modelsLimit, cursor: $modelsCursor, filter: $modelsFilter) {
            items {
              id
              name
              previewUrl
              updatedAt
              displayName
              description
              createdAt
            }
            cursor
            totalCount
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
          modelsLimit,
          modelsCursor,
          modelsFilter,
        },
      };

    var response = await _client
      .ExecuteGraphQLRequest<RequiredResponse<ProjectWithModels>>(request, cancellationToken)
      .ConfigureAwait(false);
    return response.data;
  }

  /// <param name="projectId"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  /// <seealso cref="Get"/>
  /// <seealso cref="GetWithModels"/>
  public async Task<ProjectWithTeam> GetWithTeam(string projectId, CancellationToken cancellationToken = default)
  {
    //language=graphql
    const string QUERY = """
      query ProjectGetWithTeam($projectId: String!) {
        data:project(id: $projectId) {
          id
          name
          description
          visibility
          allowPublicComments
          role
          createdAt
          updatedAt
          team {
            role
            user {
              id
              name
              bio
              company
              avatar
              verified
              role
            }
          }
          invitedTeam {
            id
            inviteId
            projectId
            projectName
            title
            role
            token
            user {
              id
              name
              bio
              company
              avatar
              verified
              role
            }
            invitedBy {
              id
              name
              bio
              company
              avatar
              verified
              role
            }
          }
          workspaceId
        }
      }
      """;
    GraphQLRequest request = new() { Query = QUERY, Variables = new { projectId } };

    var response = await _client
      .ExecuteGraphQLRequest<RequiredResponse<ProjectWithTeam>>(request, cancellationToken)
      .ConfigureAwait(false);
    return response.data;
  }

  /// <param name="input"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task<Project> Create(ProjectCreateInput input, CancellationToken cancellationToken = default)
  {
    //language=graphql
    const string QUERY = """
      mutation ProjectCreate($input: ProjectCreateInput) {
        data:projectMutations {
          data:create(input: $input) {
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
      """;
    GraphQLRequest request = new() { Query = QUERY, Variables = new { input } };

    var response = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<Project>>>(request, cancellationToken)
      .ConfigureAwait(false);
    return response.data.data;
  }

  /// <param name="input"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task<Project> Update(ProjectUpdateInput input, CancellationToken cancellationToken = default)
  {
    //language=graphql
    const string QUERY = """
      mutation ProjectUpdate($input: ProjectUpdateInput!) {
        data:projectMutations{
          data:update(update: $input) {
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
        }
      }
      """;
    GraphQLRequest request = new() { Query = QUERY, Variables = new { input } };

    var response = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<Project>>>(request, cancellationToken)
      .ConfigureAwait(false);
    return response.data.data;
  }

  /// <param name="projectId">The id of the Project to delete</param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task<bool> Delete(string projectId, CancellationToken cancellationToken = default)
  {
    //language=graphql
    const string QUERY = """
      mutation ProjectDelete($projectId: String!) {
        data:projectMutations {
          data:delete(id: $projectId)
        }
      }
      """;
    GraphQLRequest request = new() { Query = QUERY, Variables = new { projectId } };

    var response = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<bool>>>(request, cancellationToken)
      .ConfigureAwait(false);
    return response.data.data;
  }

  /// <param name="input"></param>
  /// <param name="cancellationToken"></param>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task<ProjectWithTeam> UpdateRole(
    ProjectUpdateRoleInput input,
    CancellationToken cancellationToken = default
  )
  {
    //language=graphql
    const string QUERY = """
      mutation ProjectUpdateRole($input: ProjectUpdateRoleInput!) {
        data:projectMutations {
          data:updateRole(input: $input) {
            id
            name
            description
            visibility
            allowPublicComments
            role
            createdAt
            updatedAt
            team {
              role
              user {
                id
                name
                bio
                company
                avatar
                verified
                role
              }
            }
            invitedTeam {
              id
              inviteId
              projectId
              projectName
              title
              role
              token
              user {
                id
                name
                bio
                company
                avatar
                verified
                role
              }
              invitedBy {
                id
                name
                bio
                company
                avatar
                verified
                role
              }
            }
            workspaceId
          }
        }
      }
      """;
    GraphQLRequest request = new() { Query = QUERY, Variables = new { input } };

    var response = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<ProjectWithTeam>>>(request, cancellationToken)
      .ConfigureAwait(false);
    return response.data.data;
  }
}
