using Speckle.Sdk.Helpers;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Serialisation.V2.Receive;
using Speckle.Sdk.Serialisation.V2.Send;
using Speckle.Sdk.SQLite;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation.V2;

public interface ISerializeProcessFactory
{
  ISerializeProcess CreateSerializeProcess(
    Uri url,
    string streamId,
    string? authorizationToken,
    IProgress<ProgressArgs>? progress,
    SerializeProcessOptions? options = null
  );
  IDeserializeProcess CreateDeserializeProcess(
    Uri url,
    string streamId,
    string? authorizationToken,
    IProgress<ProgressArgs>? progress,
    DeserializeProcessOptions? options = null
  );

  public ISerializeProcess CreateSerializeProcess(
    SerializeProcessOptions? options = null,
    IProgress<ProgressArgs>? progress = null
  );
}

public class SerializeProcessFactory(
  ISpeckleHttp speckleHttp,
  ISdkActivityFactory activityFactory,
  IBaseChildFinder baseChildFinder,
  IObjectSerializerFactory objectSerializerFactory,
  IObjectDeserializerFactory objectDeserializerFactory,
  ISqLiteJsonCacheManagerFactory sqLiteJsonCacheManagerFactory
) : ISerializeProcessFactory
{
  public ISerializeProcess CreateSerializeProcess(
    Uri url,
    string streamId,
    string? authorizationToken,
    IProgress<ProgressArgs>? progress,
    SerializeProcessOptions? options = null
  )
  {
    var sqLiteJsonCacheManager = sqLiteJsonCacheManagerFactory.CreateFromStream(streamId);
    var serverObjectManager = new ServerObjectManager(speckleHttp, activityFactory, url, streamId, authorizationToken);
    return new SerializeProcess(
      progress,
      sqLiteJsonCacheManager,
      serverObjectManager,
      baseChildFinder,
      objectSerializerFactory,
      options
    );
  }
  
  public ISerializeProcess CreateSerializeProcess(
    SerializeProcessOptions? options = null,
    IProgress<ProgressArgs>? progress = null
  )
  {
    var sqLiteJsonCacheManager = new DummySqLiteJsonCacheManager();
    var serverObjectManager = new DummySendServerObjectManager();
    return new SerializeProcess(
      progress,
      sqLiteJsonCacheManager,
      serverObjectManager,
      baseChildFinder,
      objectSerializerFactory,
      options
    );
  }

  public IDeserializeProcess CreateDeserializeProcess(
    Uri url,
    string streamId,
    string? authorizationToken,
    IProgress<ProgressArgs>? progress,
    DeserializeProcessOptions? options = null
  )
  {
    var sqLiteJsonCacheManager = sqLiteJsonCacheManagerFactory.CreateFromStream(streamId);
    var serverObjectManager = new ServerObjectManager(speckleHttp, activityFactory, url, streamId, authorizationToken);

    var objectLoader = new ObjectLoader(sqLiteJsonCacheManager, serverObjectManager, progress);
    return new DeserializeProcess(progress, objectLoader, objectDeserializerFactory, options);
  }
}
