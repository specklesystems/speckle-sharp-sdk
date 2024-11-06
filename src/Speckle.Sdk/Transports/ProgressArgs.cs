namespace Speckle.Sdk.Transports;

public readonly record struct ProgressArgs(ProgressEvent ProgressEvent, long Count, long? Total);

public enum ProgressEvent
{
  CachedToLocal, //send and receive

  FromCacheOrSerialized,
  FindingChildren,
  UploadBytes,
  UploadedObjects,

  CacheCheck,
  DownloadBytes,
  DeserializeObject,

  SerializeObject, // old
}
