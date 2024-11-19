using GraphQL;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Models.Responses;

namespace Speckle.Sdk.Api.GraphQL.Resources;

public sealed class ModelResource
{
  private readonly ISpeckleGraphQLClient _client;

  internal ModelResource(ISpeckleGraphQLClient client)
  {
    _client = client;
  }

  /// <param name="modelId"></param>
  /// <param name="projectId"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  /// <seealso cref="GetWithVersions"/>
  public async Task<Model> Get(string modelId, string projectId, CancellationToken cancellationToken = default)
  {
    //language=graphql
    const string QUERY = """
      query ModelGet($modelId: String!, $projectId: String!) {
        data:project(id: $projectId) {
          data:model(id: $modelId) {
            id
            name
            previewUrl
            updatedAt
            description
            displayName
            createdAt
            author {
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
      }
      """;
    var request = new GraphQLRequest { Query = QUERY, Variables = new { modelId, projectId } };

    var response = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<Model>>>(request, cancellationToken)
      .ConfigureAwait(false);

    return response.data.data;
  }

  /// <param name="projectId"></param>
  /// <param name="modelId"></param>
  /// <param name="versionsLimit">Max number of versions to fetch</param>
  /// <param name="versionsCursor">Optional cursor for pagination</param>
  /// <param name="versionsFilter">Optional versions filter</param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  /// <see cref="Get"/>
  public async Task<ModelWithVersions> GetWithVersions(
    string modelId,
    string projectId,
    int versionsLimit = ServerLimits.DEFAULT_PAGINATION_REQUEST,
    string? versionsCursor = null,
    ModelVersionsFilter? versionsFilter = null,
    CancellationToken cancellationToken = default
  )
  {
    //language=graphql
    const string QUERY = """
      query ModelGetWithVersions($modelId: String!, $projectId: String!, $versionsLimit: Int!, $versionsCursor: String, $versionsFilter: ModelVersionsFilter) {
        data:project(id: $projectId) {
          data:model(id: $modelId) {
            id
            name
            previewUrl
            updatedAt
            versions(limit: $versionsLimit, cursor: $versionsCursor, filter: $versionsFilter) {
              items {
                id
                referencedObject
                message
                sourceApplication
                createdAt
                previewUrl
                authorUser {
                  avatar
                  id
                  name
                  bio
                  company
                  verified
                  role
                }
              }
              totalCount
              cursor
            }
            description
            displayName
            createdAt
            author {
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
      }
      """;

    var request = new GraphQLRequest
    {
      Query = QUERY,
      Variables = new
      {
        projectId,
        modelId,
        versionsLimit,
        versionsCursor,
        versionsFilter,
      },
    };

    var response = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<ModelWithVersions>>>(request, cancellationToken)
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
  public async Task<ResourceCollection<Model>> GetModels(
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
          data:models(limit: $modelsLimit, cursor: $modelsCursor, filter: $modelsFilter) {
            items {
              id
              name
              previewUrl
              updatedAt
              displayName
              description
              createdAt
              author {
                avatar
                bio
                company
                id
                name
                role
                verified
              }
            }
            totalCount
            cursor
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
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<ResourceCollection<Model>>>>(request, cancellationToken)
      .ConfigureAwait(false);
    return response.data.data;
  }

  /// <param name="input"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task<Model> Create(CreateModelInput input, CancellationToken cancellationToken = default)
  {
    //language=graphql
    const string QUERY = """
      mutation ModelCreate($input: CreateModelInput!) {
        data:modelMutations {
          data:create(input: $input) {
            id
            displayName
            name
            description
            createdAt
            updatedAt
            previewUrl
            author {
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
      }
      """;

    GraphQLRequest request = new() { Query = QUERY, Variables = new { input } };

    var res = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<Model>>>(request, cancellationToken)
      .ConfigureAwait(false);

    return res.data.data;
  }

  /// <param name="input"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task Delete(DeleteModelInput input, CancellationToken cancellationToken = default)
  {
    //language=graphql
    const string QUERY = """
      mutation ModelDelete($input: DeleteModelInput!) {
        data:modelMutations {
          data:delete(input: $input)
        }
      }
      """;

    GraphQLRequest request = new() { Query = QUERY, Variables = new { input } };

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
  public async Task<Model> Update(UpdateModelInput input, CancellationToken cancellationToken = default)
  {
    //language=graphql
    const string QUERY = """
      mutation ModelUpdate($input: UpdateModelInput!) {
        data:modelMutations {
          data:update(input: $input) {
            id
            name
            displayName
            description
            createdAt
            updatedAt
            previewUrl
            author {
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
      }
      """;

    GraphQLRequest request = new() { Query = QUERY, Variables = new { input } };

    var res = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<Model>>>(request, cancellationToken)
      .ConfigureAwait(false);

    return res.data.data;
  }
}
