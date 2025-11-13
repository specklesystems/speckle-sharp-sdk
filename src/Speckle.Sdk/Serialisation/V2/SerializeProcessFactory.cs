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
  IServerBlobManagerFactory serverBlobManagerFactory,
  ILoggerFactory loggerFactory
) : ISerializeProcessFactory
{
  public ISerializeProcess CreateSerializeProcess(
    Uri url,
    string projectId,
    string? authorizationToken,
    IProgress<ProgressArgs>? progress,
    CancellationToken cancellationToken,
    SerializeProcessOptions? options = null
  )
  {
    var sqLiteJsonCacheManager = sqLiteJsonCacheManagerFactory.CreateFromStream(projectId);
    var serverObjectManager = serverObjectManagerFactory.Create(url, projectId, authorizationToken);
    var serverBlobManager = serverBlobManagerFactory.Create(url, projectId, authorizationToken);
    return CreateSerializeProcess(sqLiteJsonCacheManager, serverObjectManager, serverBlobManager, progress, cancellationToken, options);
  }

  public ISerializeProcess CreateSerializeProcess(
    ISqLiteJsonCacheManager sqLiteJsonCacheManager,
    IServerObjectManager serverObjectManager,
    IServerBlobManager serverBlobManager,
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
        serverBlobManager,
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
      null!, //this would need a better solution
      progress,
      cancellationToken,
      options
    );
  }
}
