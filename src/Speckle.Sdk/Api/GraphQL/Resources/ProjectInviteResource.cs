using GraphQL;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Models.Responses;

namespace Speckle.Sdk.Api.GraphQL.Resources;

public sealed class ProjectInviteResource
{
  private readonly ISpeckleGraphQLClient _client;

  internal ProjectInviteResource(ISpeckleGraphQLClient client)
  {
    _client = client;
  }

  /// <param name="projectId"></param>
  /// <param name="input"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task<ProjectWithTeam> Create(
    string projectId,
    ProjectInviteCreateInput input,
    CancellationToken cancellationToken = default
  )
  {
    //language=graphql
    const string QUERY = """
      mutation ProjectInviteCreate($projectId: ID!, $input: ProjectInviteCreateInput!) {
        data:projectMutations {
          data:invites {
            data:create(projectId: $projectId, input: $input) {
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
            }
          }
        }
      }
      """;
    GraphQLRequest request = new() { Query = QUERY, Variables = new { projectId, input } };

    var response = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<RequiredResponse<ProjectWithTeam>>>>(
        request,
        cancellationToken
      )
      .ConfigureAwait(false);

    return response.data.data.data;
  }

  /// <param name="input"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task<bool> Use(ProjectInviteUseInput input, CancellationToken cancellationToken = default)
  {
    //language=graphql
    const string QUERY = """
      mutation ProjectInviteUse($input: ProjectInviteUseInput!) {
        data:projectMutations {
          data:invites {
            data:use(input: $input)
          }
        }
      }
      """;
    GraphQLRequest request = new() { Query = QUERY, Variables = new { input } };

    var response = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<RequiredResponse<bool>>>>(request, cancellationToken)
      .ConfigureAwait(false);
    return response.data.data.data;
  }

  /// <param name="projectId"></param>
  /// <param name="token"></param>
  /// <param name="cancellationToken"></param>
  /// <returns>The invite, or null if no invite exists</returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task<PendingStreamCollaborator?> Get(
    string projectId,
    string? token,
    CancellationToken cancellationToken = default
  )
  {
    //language=graphql
    const string QUERY = """
      query ProjectInvite($projectId: String!, $token: String) {
        data:projectInvite(projectId: $projectId, token: $token) {
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
            avatar
            bio
            company
            id
            name
            role
            verified
          }
        }
      }
      """;
    GraphQLRequest request = new() { Query = QUERY, Variables = new { projectId, token } };

    var response = await _client
      .ExecuteGraphQLRequest<OptionalResponse<PendingStreamCollaborator>>(request, cancellationToken)
      .ConfigureAwait(false);

    return response.data;
  }

  /// <param name="projectId"></param>
  /// <param name="inviteId"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task<ProjectWithTeam> Cancel(
    string projectId,
    string inviteId,
    CancellationToken cancellationToken = default
  )
  {
    //language=graphql
    const string QUERY = """
      mutation ProjectInviteCancel($projectId: ID!, $inviteId: String!) {
        data:projectMutations {
          data:invites {
            data:cancel(projectId: $projectId, inviteId: $inviteId) {
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
      }
      """;
    GraphQLRequest request = new() { Query = QUERY, Variables = new { projectId, inviteId } };

    var response = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<RequiredResponse<ProjectWithTeam>>>>(
        request,
        cancellationToken
      )
      .ConfigureAwait(false);

    return response.data.data.data;
  }
}
