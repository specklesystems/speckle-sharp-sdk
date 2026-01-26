using System.Diagnostics;
using GraphQL;
using Speckle.Sdk.Api.Blob;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Models.Responses;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Api.GraphQL.Resources;

public sealed class FileImportResource : IDisposable
{
  private readonly ISpeckleGraphQLClient _client;
  private readonly IBlobApi _blobApi;

  internal FileImportResource(ISpeckleGraphQLClient client, IBlobApi blobApi)
  {
    _client = client;
    _blobApi = blobApi;
  }

  /// <summary>
  /// This is mostly an internal api, that marks a file import job finished.
  /// </summary>
  /// <param name="input">Either <see cref="FileImportSuccessInput"/> or <see cref="FileImportErrorInput"/></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  /// <remarks>
  /// Only use this if you are writing a file importer, that is responsible for
  /// processing file import jobs.
  /// Only works on servers version >=2.25.8 but from 3.0.7 onwards has been deprecated and replaced by model ingestion api
  /// see <see cref="ModelIngestionResource.Complete"/>
  /// </remarks>
  [Obsolete(FileImportInputBase.FILE_IMPORT_DEPRECATION_MESSAGE)]
  public async Task<bool> FinishFileImportJob(FileImportInputBase input, CancellationToken cancellationToken)
  {
    //language=graphql
    const string QUERY = """
      mutation FinishFileImport($input: FinishFileImportInput!) {
          data:fileUploadMutations {
              data:finishFileImport(input: $input)
          }
      }
      """;
    var request = new GraphQLRequest { Query = QUERY, Variables = new { input } };

    var response = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<bool>>>(request, cancellationToken)
      .ConfigureAwait(false);

    return response.data.data;
  }

  /// <summary>
  ///
  /// </summary>
  /// <param name="input"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  /// <remarks>
  /// Only works on servers version >=2.25.8 but from 3.0.7 onwards has been deprecated and replaced by model ingestion api
  /// see <see cref="ModelIngestionResource.StartProcessing"/>
  /// </remarks>
  [Obsolete(FileImportInputBase.FILE_IMPORT_DEPRECATION_MESSAGE)]
  public async Task<FileImport> StartFileImportJob(
    StartFileImportInput input,
    CancellationToken cancellationToken = default
  )
  {
    //language=graphql
    const string QUERY = """
      mutation StartFileImport($input: StartFileImportInput!) {
          data:fileUploadMutations {
              data:startFileImport(input: $input) {
                  id
                  projectId
                  convertedVersionId
                  userId
                  convertedStatus
                  convertedMessage
                  modelId
                  updatedAt
              }
          }
      }
      """;
    var request = new GraphQLRequest { Query = QUERY, Variables = new { input } };

    var response = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<FileImport>>>(request, cancellationToken)
      .ConfigureAwait(false);

    return response.data.data;
  }

  /// <summary>
  /// Get a file upload url from the Speckle server.
  /// This method asks the server to create a pre-signed S3 url,
  /// which can be used as a short term authenticated route, to put a file to the server.
  /// </summary>
  /// <param name="input"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  /// <remarks>Only works on servers version >=2.25.8</remarks>
  public async Task<FileUploadUrl> GenerateUploadUrl(
    GenerateFileUploadUrlInput input,
    CancellationToken cancellationToken = default
  )
  {
    //language=graphql
    const string QUERY = """
      mutation GenerateUploadUrl($input: GenerateFileUploadUrlInput!) {
          data:fileUploadMutations {
              data:generateUploadUrl(input: $input) {
                  fileId
                  url
              }
          }
      }
      """;
    var request = new GraphQLRequest { Query = QUERY, Variables = new { input } };

    var response = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<FileUploadUrl>>>(request, cancellationToken)
      .ConfigureAwait(false);

    return response.data.data;
  }

  /// <inheritdoc cref="Blob.BlobApi.UploadFile"/>
  [DebuggerStepThrough]
  public Task<string> UploadFile(
    string filePath,
    Uri url,
    IProgress<ProgressArgs>? progress = null,
    CancellationToken cancellationToken = default
  ) => _blobApi.UploadFile(filePath, url, progress, cancellationToken);

  /// <inheritdoc cref="Blob.BlobApi.DownloadBlob"/>
  [DebuggerStepThrough]
  public Task DownloadFile(
    string projectId,
    string fileId,
    string targetFile,
    IProgress<ProgressArgs>? progress = null,
    CancellationToken cancellationToken = default
  ) => _blobApi.DownloadBlob(projectId, fileId, targetFile, progress, cancellationToken);

  /// <param name="projectId"></param>
  /// <param name="modelId"></param>
  /// <param name="limit"></param>
  /// <param name="cursor"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  /// <remarks>Only works on servers version >=2.25.8</remarks>
  public async Task<ResourceCollection<FileImport>> GetModelFileImportJobs(
    string projectId,
    string modelId,
    int limit = ServerLimits.DEFAULT_PAGINATION_REQUEST,
    string? cursor = null,
    CancellationToken cancellationToken = default
  )
  {
    //language=graphql
    const string QUERY = """
      query ModelFileImportJobs(
          $projectId: String!,
          $modelId: String!,
          $input: GetModelUploadsInput
      ) {
        data:project(id: $projectId) {
          data:model(id: $modelId) {
              data:uploads(input: $input) {
                  totalCount
                  cursor
                  items {
                      id
                      projectId
                      convertedVersionId
                      userId
                      convertedStatus
                      convertedMessage
                      modelId
                      updatedAt
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
        projectId,
        modelId,
        input = new { limit, cursor },
      },
    };

    var response = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<RequiredResponse<ResourceCollection<FileImport>>>>>(
        request,
        cancellationToken
      )
      .ConfigureAwait(false);

    return response.data.data.data;
  }

  public void Dispose()
  {
    _blobApi.Dispose();
  }
}
