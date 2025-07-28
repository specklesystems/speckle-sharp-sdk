namespace Speckle.Sdk.Api.GraphQL.Models;

public sealed class FileImport
{
  public string id { get; init; }
  public string projectId { get; init; }
  public string? convertedVersionId { get; init; }
  public string userId { get; init; }
  public int convertedStatus { get; init; }
  public string? convertedMessage { get; init; }
  public string? modelId { get; init; }
  public DateTime updatedAt { get; init; }
}
