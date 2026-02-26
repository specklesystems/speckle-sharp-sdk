using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;

namespace Speckle.Sdk.Pipelines.Send;

public record UploadItem(string Id, Json Json, string SpeckleType, ObjectReference Reference);

internal record PresignedUploadResponse
{
  public required Uri Url { get; init; }
  public required string Key { get; init; }
  public Dictionary<string, string> AdditionalRequestHeaders { get; init; } = new();
}

internal readonly struct TriggerUploadRequest
{
  [JsonProperty("etag")]
  public required string Etag { get; init; }
}
