using Speckle.Sdk.Helpers;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Serialisation.V2.Receive;
using Speckle.Sdk.Serialisation.V2.Send;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation.V2;

public interface ISerializeProcessFactory
{
  ISerializeProcess CreateSerializeProcess(
    Uri url,
    string streamId,
    string? authorizationToken,
    IProgress<ProgressArgs>? progress
  );
  IDeserializeProcess CreateDeserializeProcess(
    Uri url,
    string streamId,
    string? authorizationToken,
    IProgress<ProgressArgs>? progress
  );
}

public class SerializeProcessFactory(
  ISpeckleHttp speckleHttp,
  ISdkActivityFactory activityFactory,
  IBaseChildFinder baseChildFinder,
  IObjectSerializerFactory objectSerializerFactory
) : ISerializeProcessFactory
{
  public ISerializeProcess CreateSerializeProcess(
    Uri url,
    string streamId,
    string? authorizationToken,
    IProgress<ProgressArgs>? progress
  )
  {
    var sqliteSendCacheManager = new SQLiteSendCacheManager(streamId);
    var serverObjectManager = new ServerObjectManager(speckleHttp, activityFactory, url, authorizationToken);
    return new SerializeProcess(
      progress,
      sqliteSendCacheManager,
      serverObjectManager,
      baseChildFinder,
      objectSerializerFactory
    );
  }

  public IDeserializeProcess CreateDeserializeProcess(
    Uri url,
    string streamId,
    string? authorizationToken,
    IProgress<ProgressArgs>? progress
  )
  {
    var sqliteSendCacheManager = new SQLiteReceiveCacheManager(streamId);
    var serverObjectManager = new ServerObjectManager(speckleHttp, activityFactory, url, authorizationToken);

    var objectLoader = new ObjectLoader(sqliteSendCacheManager, serverObjectManager, streamId, progress);
    return new DeserializeProcess(progress, objectLoader);
  }
}
