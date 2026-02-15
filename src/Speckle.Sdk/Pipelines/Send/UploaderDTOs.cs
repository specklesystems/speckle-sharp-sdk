using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;

namespace Speckle.Sdk.Pipelines.Send;

public record UploadItem(string Id, Json Json, string SpeckleType, ObjectReference Reference);

internal record PresignedUploadResponse
{
  public required Uri Url { get; init; }
  public required string Key { get; init; }
}

internal record ProcessUploadRequest
{
  public required string key { get; init; }
  public required string ingestionId { get; init; }
}

internal record ProcessUploadResponse
{
  public required string ingestionId { get; init; }
}

internal record UploadResult
{
  public required string IngestionId { get; init; }
}
