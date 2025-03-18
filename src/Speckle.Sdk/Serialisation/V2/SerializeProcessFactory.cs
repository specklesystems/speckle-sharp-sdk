using Microsoft.Extensions.Logging;
using Speckle.Sdk.Serialisation.V2.Receive;
using Speckle.Sdk.Serialisation.V2.Send;
using Speckle.Sdk.SQLite;

namespace Speckle.Sdk.Serialisation.V2;

public interface ISerializeProcessFactory
{
  ISerializeProcess CreateSerializeProcess(
    Uri url,
    string streamId,
    string? authorizationToken,
    IProgress<ProgressArgs>? progress,
    CancellationToken cancellationToken,
    SerializeProcessOptions? options = null
  );
  IDeserializeProcess CreateDeserializeProcess(
    Uri url,
    string streamId,
    string? authorizationToken,
    IProgress<ProgressArgs>? progress,
    CancellationToken cancellationToken,
    DeserializeProcessOptions? options = null
  );
}

public class SerializeProcessFactory(
  IBaseChildFinder baseChildFinder,
  IObjectSerializerFactory objectSerializerFactory,
  IBaseDeserializer baseDeserializer,
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
    return new SerializeProcess(
      progress,
      sqLiteJsonCacheManager,
      serverObjectManager,
      baseChildFinder,
      new BaseSerializer(sqLiteJsonCacheManager, objectSerializerFactory),
      loggerFactory,
      cancellationToken,
      options
    );
  }

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
}
