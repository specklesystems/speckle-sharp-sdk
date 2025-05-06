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
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task<ProjectPermissionChecks> GetPermissions(
    string projectId,
    CancellationToken cancellationToken = default
  )
  {
    //language=graphql
    const string QUERY = """
      query Project($projectId: String!) {
        data:project(id: $projectId) {
          data:permissions {
            canCreateModel {
              authorized
              code
              message
            }
            canDelete {
              authorized
              code
              message
            }
          }
        }
      }
      """;
    GraphQLRequest request = new() { Query = QUERY, Variables = new { projectId } };

    var response = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<ProjectPermissionChecks>>>(request, cancellationToken)
      .ConfigureAwait(false);
    return response.data.data;
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
    GraphQLRequest request = new()
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
          workspaceId
          sourceApps
          team {
            id
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

  /// <summary>
  /// Creates a non-workspace project (aka Personal Project)<br/>
  /// See <see cref="ActiveUserResource.CanCreatePersonalProjects"/> to see if the user has permission
  /// </summary>
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

  /// <summary>
  /// Creates a workspace project.<br/>
  /// This feature is only supported on Workspace Enabled Servers (e.g. app.speckle.systems)
  /// See <see cref="ActiveUserResource.CanCreatePersonalProjects"/> to see if the user has permission
  /// </summary>
  /// <param name="input"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task<Project> CreateInWorkspace(
    WorkspaceProjectCreateInput input,
    CancellationToken cancellationToken = default
  )
  {
    //language=graphql
    const string QUERY = """
      mutation WorkspaceProjectCreate($input: WorkspaceProjectCreateInput!) {
        data:workspaceMutations {
          data:projects {
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
      }
      """;
    GraphQLRequest request = new() { Query = QUERY, Variables = new { input } };

    var response = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<RequiredResponse<Project>>>>(request, cancellationToken)
      .ConfigureAwait(false);
    return response.data.data.data;
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
  public async Task Delete(string projectId, CancellationToken cancellationToken = default)
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

    if (!response.data.data)
    {
      //This should never happen, the server should never return `false` without providing a reason
      throw new InvalidOperationException("GraphQL data did not indicate success, but no GraphQL error was provided");
    }
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
            sourceApps
            workspaceId
            team {
              id
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
