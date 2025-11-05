using GraphQL;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Models.Responses;
using Version = Speckle.Sdk.Api.GraphQL.Models.Version;

namespace Speckle.Sdk.Api.GraphQL.Resources;

public sealed class VersionResource
{
  private readonly ISpeckleGraphQLClient _client;

  internal VersionResource(ISpeckleGraphQLClient client)
  {
    _client = client;
  }

  /// <param name="projectId"></param>
  /// <param name="versionId"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task<Version> Get(string versionId, string projectId, CancellationToken cancellationToken = default)
  {
    //language=graphql
    const string QUERY = """
      query VersionGet($projectId: String!, $versionId: String!) {
        data:project(id: $projectId) {
          data:version(id: $versionId) {
            id
            referencedObject
            message
            sourceApplication
            createdAt
            previewUrl
            authorUser {
              id
              name
              bio
              company
              verified
              role
              avatar
            }
          }
        }
      }
      """;
    GraphQLRequest request = new() { Query = QUERY, Variables = new { projectId, versionId } };

    var response = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<Version>>>(request, cancellationToken)
      .ConfigureAwait(false);
    return response.data.data;
  }

  /// <param name="projectId"></param>
  /// <param name="modelId"></param>
  /// <param name="limit">Max number of versions to fetch</param>
  /// <param name="cursor">Optional cursor for pagination</param>
  /// <param name="filter">Optional filter</param>
  /// <param name="cancellationToken"></param>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task<ResourceCollection<Version>> GetVersions(
    string modelId,
    string projectId,
    int limit = ServerLimits.DEFAULT_PAGINATION_REQUEST,
    string? cursor = null,
    ModelVersionsFilter? filter = null,
    CancellationToken cancellationToken = default
  )
  {
    //language=graphql
    const string QUERY = """
      query VersionGetVersions($projectId: String!, $modelId: String!, $limit: Int!, $cursor: String, $filter: ModelVersionsFilter) {
        data:project(id: $projectId) {
          data:model(id: $modelId) {
            data:versions(limit: $limit, cursor: $cursor, filter: $filter) {
              items {
                id
                referencedObject
                message
                sourceApplication
                createdAt
                previewUrl
                authorUser {
                  id
                  name
                  bio
                  company
                  verified
                  role
                  avatar
                }
              }
              cursor
            }
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
        modelId,
        limit,
        cursor,
        filter,
      },
    };

    var response = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<RequiredResponse<ResourceCollection<Version>>>>>(
        request,
        cancellationToken
      )
      .ConfigureAwait(false);
    return response.data.data.data;
  }

  /// <param name="input"></param>
  /// <param name="cancellationToken"></param>
  /// <returns>The created <see cref="Version"/></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task<Version> Create(CreateVersionInput input, CancellationToken cancellationToken = default)
  {
    //language=graphql
    const string QUERY = """
      mutation Create($input: CreateVersionInput!) {
        data:versionMutations {
          data:create(input: $input) {
            id
            referencedObject
            message
            sourceApplication
            createdAt
            previewUrl
            authorUser {
              id
              name
              bio
              company
              verified
              role
              avatar
            }
          }
        }
      }
      """;

    GraphQLRequest request = new() { Query = QUERY, Variables = new { input } };

    var response = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<Version>>>(request, cancellationToken)
      .ConfigureAwait(false);
    return response.data.data;
  }

  /// <param name="input"></param>
  /// <param name="cancellationToken"></param>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task<Version> Update(UpdateVersionInput input, CancellationToken cancellationToken = default)
  {
    //language=graphql
    const string QUERY = """
      mutation VersionUpdate($input: UpdateVersionInput!) {
        data:versionMutations {
          data:update(input: $input) {
            id
            referencedObject
            message
            sourceApplication
            createdAt
            previewUrl
            authorUser {
              id
              name
              bio
              company
              verified
              role
              avatar
            }
          }
        }
      }
      """;
    GraphQLRequest request = new() { Query = QUERY, Variables = new { input } };

    var response = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<Version>>>(request, cancellationToken)
      .ConfigureAwait(false);
    return response.data.data;
  }

  /// <param name="input"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task<string> MoveToModel(MoveVersionsInput input, CancellationToken cancellationToken = default)
  {
    //language=graphql
    const string QUERY = """
      mutation VersionMoveToModel($input: MoveVersionsInput!) {
        data:versionMutations {
          data:moveToModel(input: $input) {
            data:id
          }
        }
      }
      """;
    GraphQLRequest request = new() { Query = QUERY, Variables = new { input } };

    var response = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<RequiredResponse<string>>>>(request, cancellationToken)
      .ConfigureAwait(false);
    return response.data.data.data;
  }

  /// <param name="input"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task Delete(DeleteVersionsInput input, CancellationToken cancellationToken = default)
  {
    //language=graphql
    const string QUERY = """
      mutation VersionDelete($input: DeleteVersionsInput!) {
        data:versionMutations {
          data:delete(input: $input)
        }
      }
      """;
    GraphQLRequest request = new() { Query = QUERY, Variables = new { input } };

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
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task Received(MarkReceivedVersionInput input, CancellationToken cancellationToken = default)
  {
    //language=graphql
    const string QUERY = """
      mutation MarkReceived($input: MarkReceivedVersionInput!) {
        data:versionMutations {
          data:markReceived(input: $input)
        }
      }
      """;
    GraphQLRequest request = new() { Query = QUERY, Variables = new { input } };

    var response = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<bool>>>(request, cancellationToken)
      .ConfigureAwait(false);

    if (!response.data.data)
    {
      //This should never happen, the server should never return `false` without providing a reason
      throw new InvalidOperationException("GraphQL data did not indicate success, but no GraphQL error was provided");
    }
  }

  [Obsolete("modelId is no longer required, use the overload that doesn't specify a model id", true)]
  public Task<Version> Get(
    string versionId,
    string modelId,
    string projectId,
    CancellationToken cancellationToken = default
  ) => throw new NotImplementedException();
}
