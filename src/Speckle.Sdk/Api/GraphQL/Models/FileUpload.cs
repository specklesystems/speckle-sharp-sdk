using Speckle.Sdk.Api.GraphQL.Enums;

namespace Speckle.Sdk.Api.GraphQL.Models;

public sealed class FileUpload
{
  public required string? convertedCommitId { get; init; }
  public required DateTime convertedLastUpdate { get; init; }
  public required string? convertedMessage { get; init; }
  public required FileUploadConversionStatus convertedStatus { get; init; }
  public required string convertedVersionId { get; init; }
  public required string fileName { get; init; }
  public required int fileSize { get; init; }
  public required string fileType { get; init; }
  public required string id { get; init; }
  public required Model? model { get; init; }
  public required string modelName { get; init; }
  public required string projectId { get; init; }
  public required bool uploadComplete { get; init; }
  public required DateTime uploadDate { get; init; }
  public required string userId { get; init; }
}
