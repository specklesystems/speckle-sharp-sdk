namespace Speckle.Sdk.Transports;

public readonly record struct ProgressArgs(ProgressEvent ProgressEvent, long Count, long? Total);

public enum ProgressEvent
{
  CacheCheck,
  Cached,
  DownloadBytes,
  UploadBytes,
  DownloadObject,
  UploadObject,
  DeserializeObject,
  SerializeObject,
}
