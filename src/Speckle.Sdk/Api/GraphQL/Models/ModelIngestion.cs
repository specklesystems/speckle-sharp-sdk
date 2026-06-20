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

  /// <summary>
  /// Server pre-allocated version id (server v2 data-endpoints). Allocated at ingestion creation so the
  /// producer can write final artefact filenames (and the artefact pipeline can sign/complete) before any
  /// bytes are produced — the same id becomes the commit PK at complete. A dedicated field, distinct from
  /// <see cref="ModelIngestionStatusData"/>'s Success-gated versionId (which only exists once the version does).
  /// <c>null</c> on older servers that mint the version id at complete time (legacy <c>SendViaIngestion</c> path).
  /// </summary>
  public string? versionId { get; init; }

  public required ModelIngestionStatusData statusData { get; init; }
}
