using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Serialisation.V2.Receive;
using Speckle.Sdk.SQLite;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation.V2;

[GenerateAutoInterface]
public class DeserializeProcessFactory(
  IBaseDeserializer baseDeserializer,
  ISqLiteJsonCacheManagerFactory sqLiteJsonCacheManagerFactory,
  IServerObjectManagerFactory serverObjectManagerFactory,
  ILoggerFactory loggerFactory
) : IDeserializeProcessFactory
{
  public IDeserializeProcess CreateDeserializeProcess(
    Uri url,
    string streamId,
    string? authorizationToken,
    IProgress<ProgressArgs>? progress,
    CancellationToken cancellationToken,
    DeserializeProcessOptions? options = null
  )
  {
    var sqLiteJsonCacheManager = sqLiteJsonCacheManagerFactory.CreateFromStream(streamId);
    var serverObjectManager = serverObjectManagerFactory.Create(url, streamId, authorizationToken);
    return new DeserializeProcess(
      sqLiteJsonCacheManager,
      serverObjectManager,
      progress,
      baseDeserializer,
      loggerFactory,
      cancellationToken,
      options
    );
  }

  public IDeserializeProcess CreateDeserializeProcess(
    ConcurrentDictionary<Id, Json> jsonCache,
    ConcurrentDictionary<string, string> objects,
    IProgress<ProgressArgs>? progress,
    CancellationToken cancellationToken,
    DeserializeProcessOptions? options = null
  ) =>
    new DeserializeProcess(
      new MemoryJsonCacheManager(jsonCache),
      new MemoryServerObjectManager(objects),
      progress,
      baseDeserializer,
      loggerFactory,
      cancellationToken,
      options
    );
}
