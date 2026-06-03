using System.Collections.Concurrent;
using System.Text.Json;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;
using Speckle.Sdk.Pipelines.Receive.JsonConverters;
#if NET6_0_OR_GREATER
using Speckle.Sdk.Dependencies;
#endif


namespace Speckle.Sdk.Pipelines.Receive;

public sealed class SpeckleObjectDeserializer
{
  private readonly JsonSerializerOptions _options;
  private readonly PackFileManager _packFileManager;
  private readonly ConcurrentDictionary<string, Base> _deserialized = new();

  public SpeckleObjectDeserializer(PackFileManager packFileManager)
  {
    _packFileManager = packFileManager;
    _options = new()
    {
      Converters =
      {
        new SpeckleObjectJsonConverter(this),
        new ChunkedDoubleListJsonConverter(packFileManager),
        new ChunkedInt32ListJsonConverter(packFileManager),
        new SpeckleMatrix4x4JsonConverter(),
        new ColorArgbConverter(),
      },
    };
  }

  //Decent but a completely serial
  public Base GetCompleteObjectsTreeSerial()
  {
    string json = _packFileManager.GetRootObjectString();
    return JsonSerializer.Deserialize<Base>(json, _options).NotNull();
  }

  public Base GetObject(string id)
  {
    if (_deserialized.TryGetValue(id, out Base? cachedValue))
    {
      return cachedValue;
    }

    string json = _packFileManager.GetObjectString(id);
    Base result = JsonSerializer.Deserialize<Base>(json, _options).NotNull();
    _deserialized[id] = result;
    return result;
  }

  public IEnumerable<Base> GetObjects(CancellationToken cancellationToken)
  {
    foreach ((string id, _, string json) in _packFileManager.GetObjects(cancellationToken))
    {
      if (_deserialized.TryGetValue(id, out Base? cachedValue))
      {
        yield return cachedValue;
      }

      Base result = JsonSerializer.Deserialize<Base>(json, _options).NotNull();
      _deserialized[id] = result;
      yield return result;
    }
  }

#if NET6_0_OR_GREATER
  public async Task<Base> GetCompleteObjectsTreeAsync(CancellationToken cancellationToken)
  {
    await Parallel
      .ForEachAsync(
        _packFileManager.GetObjectsAsync(cancellationToken),
        new ParallelOptions() { MaxDegreeOfParallelism = 6 },
        (item, cancellationToken) =>
        {
          if (!_deserialized.ContainsKey(item.id))
          {
            Base result = JsonSerializer.Deserialize<Base>(item.json, _options).NotNull();
            _deserialized[item.id] = result;
          }

          return default;
        }
      )
      .ConfigureAwait(false);

    string rootId = _packFileManager.GetRootObjectId();
    return GetObject(rootId);
  }
#else
  public Task<Base> GetCompleteObjectsTreeAsync(CancellationToken cancellationToken)
  {
    throw new NotImplementedException();
  }
#endif

#if NET6_0_OR_GREATER
  public async Task<Base> ChannelCompleteObjectsTreeAsync(
    CancellationToken cancellationToken,
    int deserializerMaxDegreeOfParallelism = 6
  )
  {
    RepackedChannel<(string id, string speckle_type, string json)> channel = new(1000, false, true);

    Task writer = Task.Run(
      async () =>
      {
        try
        {
          await foreach (var item in _packFileManager.GetObjectsAsync(cancellationToken).ConfigureAwait(false))
          {
            if (!_deserialized.ContainsKey(item.id))
            {
              await channel.WriteAsync(item, cancellationToken).ConfigureAwait(false);
            }
          }

          channel.CompleteWriter();
        }
        catch (Exception ex)
        {
          channel.CompleteWriter(ex);
          throw;
        }
      },
      cancellationToken
    );

    var options = new ParallelOptions()
    {
      MaxDegreeOfParallelism = deserializerMaxDegreeOfParallelism,
      CancellationToken = cancellationToken,
    };

    await Parallel.ForEachAsync(channel.ReadAllAsync(cancellationToken), options, Foo).ConfigureAwait(false);

    await writer.ConfigureAwait(false);

    string rootId = _packFileManager.GetRootObjectId();
    return GetObject(rootId);

    ValueTask Foo((string id, string speckle_type, string json) item, CancellationToken ct)
    {
      ct.ThrowIfCancellationRequested();

      if (!_deserialized.ContainsKey(item.id))
      {
        Base result = JsonSerializer.Deserialize<Base>(item.json, _options).NotNull();
        _deserialized[item.id] = result;
      }

      return ValueTask.CompletedTask;
    }
  }
#else
  public Task<Base> ChannelCompleteObjectsTreeAsync(
    CancellationToken cancellationToken,
    int deserializerMaxDegreeOfParallelism = 6
  )
  {
    throw new NotImplementedException();
  }
#endif

  //To Test
  public Base GetCompleteObjectsTreeSync(CancellationToken cancellationToken)
  {
    Parallel.ForEach(
      _packFileManager.GetObjects(cancellationToken),
      new ParallelOptions() { MaxDegreeOfParallelism = 6, CancellationToken = cancellationToken },
      (item) =>
      {
        if (!_deserialized.ContainsKey(item.id))
        {
          Base result = JsonSerializer.Deserialize<Base>(item.json, _options).NotNull();
          _deserialized[item.id] = result;
        }
      }
    );

    string rootId = _packFileManager.GetRootObjectId();
    return GetObject(rootId);
  }
}
