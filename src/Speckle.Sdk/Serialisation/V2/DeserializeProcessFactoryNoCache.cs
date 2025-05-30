using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Serialisation.V2.Receive;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation.V2;

/// <summary>
/// A version of <seealso cref="DeserializeProcessFactory"/> but without any SQLite usage.
/// This class doesn't have a matching <seealso cref="GenerateAutoInterfaceAttribute"/>, so will not be registered by <seealso cref="ServiceRegistration"/> automatically
/// Instead consumers can register this to override the default <seealso cref="DeserializeProcessFactory"/>
/// </summary>
/// <seealso cref="DeserializeProcessFactory"/>
/// <seealso cref="DeserializeProcess"/>
public sealed class DeserializeProcessFactoryNoCache(
  IBaseDeserializer baseDeserializer,
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
    var sqLiteJsonCacheManager = new MemoryJsonCacheManager(new());
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
