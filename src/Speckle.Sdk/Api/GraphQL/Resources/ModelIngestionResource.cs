using GraphQL;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Models.Responses;

namespace Speckle.Sdk.Api.GraphQL.Resources;

/// <remarks>
/// Model Ingestion API is available for server versions <c>3.0.3-alpha.583</c> and above
/// </remarks>
public sealed class ModelIngestionResource
{
  private readonly ISpeckleGraphQLClient _client;

  internal ModelIngestionResource(ISpeckleGraphQLClient client)
  {
    _client = client;
  }

  /// <summary>
  /// Create a new model ingestion
  /// </summary>
  /// <remarks>
  /// The model ingestion created will have a <c>processing</c> state (not <c>queued</c>). This mutation is designed to be used
  /// by client/connectors that are immediately processing
  /// Model Ingestion API is available for server versions <c>3.0.3-alpha.583</c> and above
  /// </remarks>
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
              modelId
              cancellationRequested
              statusData {
                ... on HasModelIngestionStatus {
                  status
                }
                ... on HasProgressMessage {
                  progressMessage
                }
              }
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

  /// <remarks>
  /// Model Ingestion API is available for server versions <c>3.0.3-alpha.583</c> and above
  /// </remarks>
  /// <param name="modelIngestionId"></param>
  /// <param name="projectId"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task<ModelIngestion> Get(
    string modelIngestionId,
    string projectId,
    CancellationToken cancellationToken = default
  )
  {
    //language=graphql
    const string QUERY = """
      query Query($projectId: String!, $modelIngestionId: ID!) {
        data:project(id: $projectId) {
          data:ingestion(id: $modelIngestionId) {
            id
            createdAt
            updatedAt
            modelId
            cancellationRequested
            statusData {
              ... on HasModelIngestionStatus {
                status
              }
              ... on HasProgressMessage {
                progressMessage
              }
            }
          }
        }
      }
      """;

    GraphQLRequest request = new() { Query = QUERY, Variables = new { projectId, modelIngestionId } };

    var res = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<ModelIngestion>>>(request, cancellationToken)
      .ConfigureAwait(false);

    return res.data.data;
  }

  /// <summary>
  /// For File Import / Cloud integrations only
  /// </summary>
  /// <remarks>
  /// Model Ingestion API is available for server versions <c>3.0.3-alpha.583</c> and above
  /// </remarks>
  /// <param name="input"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task<ModelIngestion> StartProcessing(
    ModelIngestionStartProcessingInput input,
    CancellationToken cancellationToken = default
  )
  {
    //language=graphql
    const string QUERY = """
      mutation IngestionStartProcessing($input: ModelIngestionStartProcessingInput!) {
        data: projectMutations {
          data: modelIngestionMutations {
            data: startProcessing(input: $input) {
              id
              createdAt
              updatedAt
              modelId
              cancellationRequested
              statusData {
                ... on HasModelIngestionStatus {
                  status
                }
                ... on HasProgressMessage {
                  progressMessage
                }
              }
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
  /// For File Import / Cloud integrations only
  /// </summary>
  /// <remarks>
  /// Model Ingestion API is available for server versions <c>3.0.3-alpha.583</c> and above
  /// </remarks>
  /// <param name="input"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task<ModelIngestion> Requeue(
    ModelIngestionRequeueInput input,
    CancellationToken cancellationToken = default
  )
  {
    //language=graphql
    const string QUERY = """
      mutation IngestionStartProcessing($input: ModelIngestionRequeueInput!) {
        data: projectMutations {
          data: modelIngestionMutations {
            data: requeue(input: $input) {
              id
              createdAt
              updatedAt
              modelId
              cancellationRequested
              statusData {
                ... on HasModelIngestionStatus {
                  status
                }
                ... on HasProgressMessage {
                  progressMessage
                }
              }
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

  /// <remarks>
  /// Model Ingestion API is available for server versions <c>3.0.3-alpha.583</c> and above
  /// </remarks>
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
              modelId
              cancellationRequested
              statusData {
                ... on HasModelIngestionStatus {
                  status
                }
                ... on HasProgressMessage {
                  progressMessage
                }
              }
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
  /// Request that the server completes the ingestion by creating a version
  /// If successful, the job will be in a terminal "successful" state.
  /// </summary>
  /// <remarks>
  /// Model Ingestion API is available for server versions <c>3.0.3-alpha.583</c> and above
  /// </remarks>
  /// <seealso cref="FailWithError"/>
  /// <seealso cref="FailWithCancel"/>
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
                  data:versionId
                }
              }
            }
          }
        }
      }
      """;

    GraphQLRequest request = new() { Query = QUERY, Variables = new { input } };

    var res = await _client
      .ExecuteGraphQLRequest<
        RequiredResponse<RequiredResponse<RequiredResponse<RequiredResponse<RequiredResponse<string>>>>>
      >(request, cancellationToken)
      .ConfigureAwait(false);

    return res.data.data.data.data.data;
  }

  /// <summary>
  /// Fail the job with an error.
  /// </summary>
  /// <remarks>
  /// For requested user cancellation, use <see cref="FailWithCancel"/> instead<br/>
  /// Model Ingestion API is available for server versions <c>3.0.3-alpha.583</c> and above
  /// </remarks>
  /// <seealso cref="FailWithCancel"/>
  /// <seealso cref="Complete"/>
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
              modelId
              cancellationRequested
              statusData {
                ... on HasModelIngestionStatus {
                  status
                }
                ... on HasProgressMessage {
                  progressMessage
                }
              }
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
  /// Fail the ingestion with a <c>canceled</c> status.
  /// This should only be done if the user has explicitly requested cancellation
  /// Other forms of cancellation use <see cref="FailWithError"/>.
  /// The ingestion should then enter a terminal "canceled" state.<br/>
  /// Model Ingestion API is available for server versions <c>3.0.3-alpha.583</c> and above
  /// </summary>
  /// <seealso cref="FailWithError"/>
  /// <seealso cref="Complete"/>
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
              modelId
              cancellationRequested
              statusData {
                ... on HasModelIngestionStatus {
                  status
                }
                ... on HasProgressMessage {
                  progressMessage
                }
              }
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
  /// Request that the <see cref="ModelIngestion"/> is canceled.
  /// </summary>
  /// <remarks>
  /// Note simply calling this mutation does not imediatly cancel, it doesn't even guarantee it will be canceled at all.
  /// It's up to the client to observe this cancellation request
  /// via <see cref="SubscriptionResource.CreateProjectModelIngestionCancellationRequestedSubscription"/>
  /// and report it as canceled via  <see cref="ModelIngestionResource.FailWithCancel"/>
  /// See "cooperative cancellation pattern"<br/>
  /// Model Ingestion API is available for server versions <c>3.0.3-alpha.583</c> and above
  /// </remarks>
  /// <seealso cref="FailWithError"/>
  /// <seealso cref="Complete"/>
  /// <param name="input"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task<ModelIngestion> RequestCancellation(
    ModelIngestionCancelledInput input,
    CancellationToken cancellationToken = default
  )
  {
    //language=graphql
    const string QUERY = """
      mutation IngestionRequestCancellation($input: ModelIngestionRequestCancellationInput!) {
        data: projectMutations {
          data: modelIngestionMutations {
            data: requestCancellation (input: $input) {
              id
              createdAt
              updatedAt
              modelId
              cancellationRequested
              statusData {
                ... on HasModelIngestionStatus {
                  status
                }
                ... on HasProgressMessage {
                  progressMessage
                }
              }
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
