using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Serialisation.V2.Send;
using Speckle.Sdk.SQLite;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation.V2;

[GenerateAutoInterface]
public class SerializeProcessFactory(
  IBaseChildFinder baseChildFinder,
  IObjectSerializerFactory objectSerializerFactory,
  ISqLiteJsonCacheManagerFactory sqLiteJsonCacheManagerFactory,
  IServerObjectManagerFactory serverObjectManagerFactory,
  ILoggerFactory loggerFactory
) : ISerializeProcessFactory
{
  public ISerializeProcess CreateSerializeProcess(
    Uri url,
    string streamId,
    string? authorizationToken,
    IProgress<ProgressArgs>? progress,
    CancellationToken cancellationToken,
    SerializeProcessOptions? options = null
  )
  {
    var sqLiteJsonCacheManager = sqLiteJsonCacheManagerFactory.CreateFromStream(streamId);
    var serverObjectManager = serverObjectManagerFactory.Create(url, streamId, authorizationToken);
    return CreateSerializeProcess(sqLiteJsonCacheManager, serverObjectManager, progress, cancellationToken, options);
  }

  public ISerializeProcess CreateSerializeProcess(
    ISqLiteJsonCacheManager sqLiteJsonCacheManager,
    IServerObjectManager serverObjectManager,
    IProgress<ProgressArgs>? progress,
    CancellationToken cancellationToken,
    SerializeProcessOptions? options = null
  ) =>
    new SerializeProcess(
      progress,
      new ObjectSaver(
        progress,
        sqLiteJsonCacheManager,
        serverObjectManager,
        loggerFactory.CreateLogger<ObjectSaver>(),
        options ?? new SerializeProcessOptions(),
        cancellationToken
      ),
      baseChildFinder,
      new BaseSerializer(sqLiteJsonCacheManager, objectSerializerFactory),
      loggerFactory,
      options ?? new SerializeProcessOptions(),
      cancellationToken
    );

  public ISerializeProcess CreateSerializeProcess(
    ConcurrentDictionary<Id, Json> jsonCache,
    ConcurrentDictionary<string, string> objects,
    IProgress<ProgressArgs>? progress,
    CancellationToken cancellationToken,
    SerializeProcessOptions? options = null
  )
  {
#pragma warning disable CA2000
    var memoryJsonCacheManager = new MemoryJsonCacheManager(jsonCache);
#pragma warning restore CA2000
    return CreateSerializeProcess(
      memoryJsonCacheManager,
      new MemoryServerObjectManager(objects),
      progress,
      cancellationToken,
      options
    );
  }
}
