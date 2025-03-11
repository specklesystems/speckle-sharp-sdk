using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
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

  public ISerializeProcess CreateSerializeProcess(
    ConcurrentDictionary<Id, Json> jsonCache,
    ConcurrentDictionary<string, string> objects,
    IProgress<ProgressArgs>? progress,
    CancellationToken cancellationToken,
    SerializeProcessOptions? options = null
  );
  
  public ISerializeProcess CreateSerializeProcess(
    ISqLiteJsonCacheManager sqLiteJsonCacheManager,
    IServerObjectManager serverObjectManager,
    IProgress<ProgressArgs>? progress,
    CancellationToken cancellationToken,
    SerializeProcessOptions? options = null
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
    return CreateSerializeProcess(
      sqLiteJsonCacheManager,
      serverObjectManager,
      progress,
      cancellationToken,
      options
    );
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
      new ObjectSaver(progress,
        sqLiteJsonCacheManager,
        serverObjectManager, loggerFactory, cancellationToken),
      baseChildFinder,
      new BaseSerializer(sqLiteJsonCacheManager, objectSerializerFactory),
      loggerFactory,
      cancellationToken,
      options
    );

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
    return new SerializeProcess(
      progress,
      new ObjectSaver(progress,
        memoryJsonCacheManager,
        new MemoryServerObjectManager(objects), loggerFactory, cancellationToken),
      baseChildFinder,
      new BaseSerializer(memoryJsonCacheManager, objectSerializerFactory),
      loggerFactory,
      cancellationToken,
      options
    );
  }
}

public sealed class MemoryJsonCacheManager(ConcurrentDictionary<Id, Json> jsonCache) : ISqLiteJsonCacheManager
{
  public void Dispose() { }

  public IReadOnlyCollection<(string Id, string Json)> GetAllObjects() =>
    jsonCache.Select(x => (x.Key.Value, x.Value.Value)).ToList();

  public void DeleteObject(string id) => jsonCache.TryRemove(new Id(id), out _);

  public string? GetObject(string id) => jsonCache.TryGetValue(new Id(id), out var json) ? json.Value : null;

  public void SaveObject(string id, string json) => jsonCache.TryAdd(new Id(id), new Json(json));

  public void UpdateObject(string id, string json) => jsonCache[new Id(id)] = new Json(json);

  public void SaveObjects(IEnumerable<(string id, string json)> items)
  {
    foreach (var (id, json) in items)
    {
      SaveObject(id, json);
    }
  }

  public bool HasObject(string objectId) => jsonCache.ContainsKey(new Id(objectId));
}

public class MemoryServerObjectManager(ConcurrentDictionary<string, string> objects) : IServerObjectManager
{
  public virtual async IAsyncEnumerable<(string, string)> DownloadObjects(
    IReadOnlyCollection<string> objectIds,
    IProgress<ProgressArgs>? progress,
    [EnumeratorCancellation] CancellationToken cancellationToken
  )
  {
    foreach(var item in objects.Where(x => objectIds.Contains(x.Key)))
    {
      cancellationToken.ThrowIfCancellationRequested();
      yield return (item.Key, item.Value);
    }
    await Task.CompletedTask.ConfigureAwait(false);
  }

  public virtual  Task<string?> DownloadSingleObject(string objectId, IProgress<ProgressArgs>? progress, CancellationToken cancellationToken) =>
  Task.FromResult(objects.TryGetValue(objectId, out var json) ? json : null);
  
  public virtual  Task<Dictionary<string, bool>> HasObjects(IReadOnlyCollection<string> objectIds, CancellationToken cancellationToken) => 
  Task.FromResult(objectIds.ToDictionary(x => x, objects.ContainsKey));

  public virtual Task UploadObjects(IReadOnlyList<BaseItem> objectToUpload, bool compressPayloads,
    IProgress<ProgressArgs>? progress,
    CancellationToken cancellationToken)
  {
    foreach (BaseItem baseItem in objectToUpload)
    {
      objects.TryAdd(baseItem.Id.Value, baseItem.Json.Value);
    }
    return Task.CompletedTask;
  }
}
