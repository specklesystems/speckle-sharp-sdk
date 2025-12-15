namespace Speckle.Sdk.Api.GraphQL.Models;

public sealed class FileImport
{
  public required string id { get; init; }
  public required string projectId { get; init; }
  public required string? convertedVersionId { get; init; }
  public required string userId { get; init; }
  public required int convertedStatus { get; init; }
  public required string? convertedMessage { get; init; }
  public required string? modelId { get; init; }
  public required DateTime updatedAt { get; init; }
}
