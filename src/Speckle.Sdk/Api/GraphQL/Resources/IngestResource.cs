using GraphQL;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Models.Responses;
using Version = Speckle.Sdk.Api.GraphQL.Models.Version;

namespace Speckle.Sdk.Api.GraphQL.Resources;

public sealed class IngestResource
{
  private readonly ISpeckleGraphQLClient _client;

  internal IngestResource(ISpeckleGraphQLClient client)
  {
    _client = client;
  }

  /// <param name="modelId"></param>
  /// <param name="projectId"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task<ResourceCollection<Ingest>> GetIngests(
    string modelId,
    string projectId,
    CancellationToken cancellationToken = default
  )
  {
    //language=graphql
    const string QUERY = """
      query GetIngest($modelId: String!, $projectId: String!) {
        data:project(id: $projectId) {
          data:model(id: $modelId) {
            data:ingests {
              cursor
              items {
                createdAt
                errorReason
                errorStacktrace
                fileName
                id
                maxIdleTimeoutMinutes
                modelId
                performanceData
                progress
                progressMessage
                projectId
                sourceApplication
                sourceApplicationVersion
                sourceFileData
                status
                updatedAt
                versionId
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
          }
        }
      }
      """;
    var request = new GraphQLRequest { Query = QUERY, Variables = new { modelId, projectId } };

    var response = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<RequiredResponse<ResourceCollection<Ingest>>>>>(
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
  public async Task<bool> Update(IngestUpdateInput input, CancellationToken cancellationToken = default)
  {
    //language=graphql
    const string QUERY = """
      mutation IngestUpdate($projectId: ID!, $input: IngestUpdateInput!) {
        data: projectMutations {
          data: ingestMutations(projectId: $projectId) {
            data: update(input: $input)
          }
        }
      }
      """;

    GraphQLRequest request = new() { Query = QUERY, Variables = new { input, input.projectId } };

    var res = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<RequiredResponse<bool>>>>(request, cancellationToken)
      .ConfigureAwait(false);

    return res.data.data.data;
  }

  /// <param name="input"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task<Ingest> Create(IngestCreateInput input, CancellationToken cancellationToken = default)
  {
    //language=graphql
    const string QUERY = """
      mutation IngestCreate($projectId: ID!, $input: IngestCreateInput!) {
        data: projectMutations {
          data:ingestMutations(projectId: $projectId) {
            data:create(input: $input)  {
              createdAt
              errorReason
              errorStacktrace
              fileName
              id
              maxIdleTimeoutMinutes
              modelId
              performanceData
              progress
              progressMessage
              projectId
              sourceApplication
              sourceApplicationVersion
              sourceFileData
              status
              updatedAt
              versionId
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
        }
      }
      """;

    GraphQLRequest request = new() { Query = QUERY, Variables = new { input, input.projectId } };

    var res = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<RequiredResponse<Ingest>>>>(request, cancellationToken)
      .ConfigureAwait(false);

    return res.data.data.data;
  }

  /// <param name="input"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task<Version> End(IngestFinishInput input, CancellationToken cancellationToken = default)
  {
    //language=graphql
    const string QUERY = """
      mutation IngestEnd($projectId: ID!, $input: IngestFinishInput!) {
        data: projectMutations {
          data:ingestMutations(projectId: $projectId) {
            data:end(input: $input) {
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
      }
      """;

    GraphQLRequest request = new() { Query = QUERY, Variables = new { input, input.projectId } };

    var res = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<RequiredResponse<Version>>>>(request, cancellationToken)
      .ConfigureAwait(false);

    return res.data.data.data;
  }

  /// <param name="input"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task<bool> Error(IngestErrorInput input, CancellationToken cancellationToken = default)
  {
    //language=graphql
    const string QUERY = """
      mutation IngestError($projectId: ID!, $input: IngestErrorInput!) {
        data: projectMutations {
          data:ingestMutations(projectId: $projectId) {
            data:error(input: $input)
          }
        }
      }
      """;

    GraphQLRequest request = new() { Query = QUERY, Variables = new { input, input.projectId } };

    var res = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<RequiredResponse<bool>>>>(request, cancellationToken)
      .ConfigureAwait(false);

    return res.data.data.data;
  }

  /// <param name="input"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task<bool> Cancel(CancelRequestInput input, CancellationToken cancellationToken = default)
  {
    //language=graphql
    const string QUERY = """
      mutation IngestCancel($projectId: ID!, $input: CancelRequestInput!) {
        data:projectMutations {
          data:ingestMutations(projectId: $projectId) {
            data:cancel(input: $input)
          }
        }
      }
      """;

    GraphQLRequest request = new() { Query = QUERY, Variables = new { input, input.projectId } };

    var res = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<RequiredResponse<bool>>>>(request, cancellationToken)
      .ConfigureAwait(false);

    return res.data.data.data;
  }
}
