namespace Speckle.Sdk.Api.GraphQL.Models;

public sealed class ModelIngestion
{
  public required string id { get; init; }
  public required DateTime createdAt { get; init; }
  public required DateTime updatedAt { get; init; }
  public required string modelId { get; init; }
  public required string projectId { get; init; }
  public required string userId { get; init; }
  public required bool cancellationRequested { get; init; }
  public required ModelIngestionStatusData statusData { get; init; }
}
