namespace Speckle.Sdk.Api.GraphQL.Models;

public sealed class Ingest
{
  public required DateTime createdAt { get; init; }
  public required string errorReason { get; init; }
  public required string errorStacktrace { get; init; }
  public required string fileName { get; init; }
  public required string id { get; init; }
  public required long maxIdleTimeoutMinutes { get; init; }
  public required string modelId { get; init; }
  public required Dictionary<string, object?> performanceData { get; init; }
  public required double progress { get; init; }
  public required string? progressMessage { get; init; }
  public required string projectId { get; init; }
  public required string sourceApplication { get; init; }
  public required string sourceApplicationVersion { get; init; }
  public required Dictionary<string, object?> sourceFileData { get; init; }
  public required string status { get; init; }
  public required DateTime updatedAt { get; init; }
  public required string versionId { get; init; }
  public required LimitedUser user { get; init; }
}
