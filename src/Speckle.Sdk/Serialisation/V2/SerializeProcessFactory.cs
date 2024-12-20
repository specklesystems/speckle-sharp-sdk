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
}

public class SerializeProcessFactory(
  IBaseChildFinder baseChildFinder,
  IObjectSerializerFactory objectSerializerFactory,
  IObjectDeserializerFactory objectDeserializerFactory,
  ISqLiteJsonCacheManagerFactory sqLiteJsonCacheManagerFactory,
  IServerObjectManagerFactory serverObjectManagerFactory
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
    var serverObjectManager = serverObjectManagerFactory.Create(url, streamId, authorizationToken);
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
    var serverObjectManager = serverObjectManagerFactory.Create(url, streamId, authorizationToken);

#pragma warning disable CA2000
    //owned by process, refactor later
    var objectLoader = new ObjectLoader(sqLiteJsonCacheManager, serverObjectManager, progress);
#pragma warning restore CA2000
    return new DeserializeProcess(progress, objectLoader, objectDeserializerFactory, options);
  }
}
