using GraphQL;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Models.Responses;

namespace Speckle.Sdk.Api.GraphQL.Resources;

public sealed class Ingestionresource
{
  private readonly ISpeckleGraphQLClient _client;

  internal Ingestionresource(ISpeckleGraphQLClient client)
  {
    _client = client;
  }

  /// <param name="input"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task<ModelIngestion> Create(
    ModelIngestionCreateInput input,
    CancellationToken cancellationToken = default
  )
  {
    //language=graphql
    const string QUERY = """
      mutation IngestionCreate($input: ModelIngestionCreateInput!) {
        data: projectMutations {
          data: modelIngestionMutations {
            data: create(input: $input) {
              id
              createdAt
              updatedAt
            }
          }
        }
      }
      """;

    GraphQLRequest request = new() { Query = QUERY, Variables = new { input } };

    var res = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<RequiredResponse<ModelIngestion>>>>(
        request,
        cancellationToken
      )
      .ConfigureAwait(false);

    return res.data.data.data;
  }

  /// <param name="input"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task<ModelIngestion> UpdateProgress(
    ModelIngestionUpdateInput input,
    CancellationToken cancellationToken = default
  )
  {
    //language=graphql
    const string QUERY = """
      mutation IngestionUpdateProgress(
        $input: ModelIngestionUpdateInput!
      ) {
        data: projectMutations {
          data: modelIngestionMutations {
            data: updateProgress(input: $input) {
              id
              createdAt
              updatedAt
            }
          }
        }
      }
      """;

    GraphQLRequest request = new() { Query = QUERY, Variables = new { input } };

    var res = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<RequiredResponse<ModelIngestion>>>>(
        request,
        cancellationToken
      )
      .ConfigureAwait(false);

    return res.data.data.data;
  }

  /// <param name="input"></param>
  /// <param name="cancellationToken"></param>
  /// <returns>The version id</returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task<string> Complete(ModelIngestionSuccessInput input, CancellationToken cancellationToken = default)
  {
    //language=graphql
    const string QUERY = """
      mutation IngestionComplete($input: ModelIngestionSuccessInput!) {
        data: projectMutations {
          data: modelIngestionMutations {
            data: completeWithVersion(input: $input) {
              data:statusData {
                ... on ModelIngestionSuccessStatus {
                  versionId
                }
              }
            }
          }
        }
      }
      """;

    GraphQLRequest request = new() { Query = QUERY, Variables = new { input } };

    var res = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<RequiredResponse<RequiredResponse<string>>>>>(
        request,
        cancellationToken
      )
      .ConfigureAwait(false);

    return res.data.data.data.data;
  }

  /// <param name="input"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task<ModelIngestion> FailWithError(
    ModelIngestionFailedInput input,
    CancellationToken cancellationToken = default
  )
  {
    //language=graphql
    const string QUERY = """
      mutation IngestionFailWithError($input: ModelIngestionFailedInput!) {
        data: projectMutations {
          data: modelIngestionMutations {
            data: failWithError(input: $input) {
              id
              createdAt
              updatedAt
            }
          }
        }
      }
      """;

    GraphQLRequest request = new() { Query = QUERY, Variables = new { input } };

    var res = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<RequiredResponse<ModelIngestion>>>>(
        request,
        cancellationToken
      )
      .ConfigureAwait(false);

    return res.data.data.data;
  }

  /// <summary>
  /// Request that the ingestion is canceled.
  /// </summary>
  /// <remarks>
  /// Note it's up to the client to observe this cancellation request
  /// via <see cref="SubscriptionResource.CreateProjectModelIngestionCancellationRequestedSubscription"/>
  /// and report it as canceled via  <see cref="IngestionResource.CreateProjectModelIngestionCancellationRequestedSubscription"/>`ingestion.fail_with_cancelled`.
  ///
  /// See "cooperative cancellation pattern"</remarks>
  /// <param name="input"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task<ModelIngestion> FailWithCancel(
    ModelIngestionCancelledInput input,
    CancellationToken cancellationToken = default
  )
  {
    //language=graphql
    const string QUERY = """
      mutation IngestionFailWithCancel($input: ModelIngestionCancelledInput!) {
        data: projectMutations {
          data: modelIngestionMutations {
            data: failWithCancel(input: $input) {
              id
              createdAt
              updatedAt
            }
          }
        }
      }
      """;

    GraphQLRequest request = new() { Query = QUERY, Variables = new { input } };

    var res = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<RequiredResponse<ModelIngestion>>>>(
        request,
        cancellationToken
      )
      .ConfigureAwait(false);

    return res.data.data.data;
  }
}
