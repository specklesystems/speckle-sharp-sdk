using System.Collections.Concurrent;
using System.Text.Json;
using Speckle.Sdk.Common;
using Speckle.Sdk.Dependencies;
using Speckle.Sdk.Models;
using Speckle.Sdk.Pipelines.Progress;
using Speckle.Sdk.Pipelines.Receive.JsonConverters;

namespace Speckle.Sdk.Pipelines.Receive;

public sealed class SpeckleObjectDeserializer
{
  private readonly JsonSerializerOptions _options;
  private readonly ParallelOptions _deserializeParallelismOptions;
  private readonly CancellationToken _cancellationToken;
  private readonly PackFileManager _packFileManager;
  private readonly ConcurrentDictionary<string, Base> _deserialized = new();

  public SpeckleObjectDeserializer(
    PackFileManager packFileManager,
    int deserializerMaxDegreeOfParallelism,
    CancellationToken cancellationToken
  )
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
    _deserializeParallelismOptions = new ParallelOptions()
    {
      MaxDegreeOfParallelism = deserializerMaxDegreeOfParallelism,
      CancellationToken = cancellationToken,
    };
    _cancellationToken = cancellationToken;
  }

  public Base GetObject(string id)
  {
    _cancellationToken.ThrowIfCancellationRequested();

    if (_deserialized.TryGetValue(id, out Base? cachedValue))
    {
      return cachedValue;
    }

    string json = _packFileManager.GetObjectData(id);
    Base result = JsonSerializer.Deserialize<Base>(json, _options).NotNull();
    _deserialized[id] = result;
    return result;
  }

  private async Task WriterThread(RepackedChannel<(string id, string speckle_type, string json)> channel)
  {
    try
    {
      await foreach (var item in _packFileManager.GetObjectsAsync(_cancellationToken).ConfigureAwait(false))
      {
        if (!_deserialized.ContainsKey(item.id))
        {
          await channel.WriteAsync(item, _cancellationToken).ConfigureAwait(false);
        }
      }

      channel.CompleteWriter();
    }
    catch (Exception ex)
    {
      channel.CompleteWriter(ex);
      throw;
    }
  }

  private void Deserialize((string id, string speckle_type, string json) item)
  {
    if (!_deserialized.ContainsKey(item.id))
    {
      Base result = JsonSerializer.Deserialize<Base>(item.json, _options).NotNull();
      _deserialized[item.id] = result;
    }
  }

  public async Task<Base> MaterializeGraphAsync(IProgress<CardProgress> deserializeProgress)
  {
    bool useParallel = _deserializeParallelismOptions.MaxDegreeOfParallelism > 1;

    _cancellationToken.ThrowIfCancellationRequested();

    long estimatedObjectCount = _packFileManager.GetEstimatedObjectCount();
    long counter = 0;

    deserializeProgress.Report(
      new($"Deserializing objects {counter:N0}/{estimatedObjectCount:N0}", (double)counter / estimatedObjectCount)
    );

    RepackedChannel<(string id, string speckle_type, string json)> channel = new(1000, !useParallel, true);

    Task writer = Task.Run(async () => await WriterThread(channel).ConfigureAwait(false), _cancellationToken);

#if NET6_0_OR_GREATER
    if (useParallel)
    {
      await Task.WhenAll(
          writer,
          Parallel.ForEachAsync(
            channel.ReadAllAsync(_cancellationToken),
            _deserializeParallelismOptions,
            (item, _) =>
            {
              Deserialize(item);

              long counterValue = Interlocked.Increment(ref counter);
              deserializeProgress.Report(
                new(
                  $"Deserializing objects {counterValue:N0}/{estimatedObjectCount:N0}",
                  (double)counterValue / estimatedObjectCount
                )
              );

              return ValueTask.CompletedTask;
            }
          )
        )
        .ConfigureAwait(false);
    }
    else
#endif
    {
      await Task.WhenAll(writer, ReadAll()).ConfigureAwait(false);
    }

    string rootId = _packFileManager.GetRootObjectId();
    return GetObject(rootId);

    async Task ReadAll()
    {
      await foreach (var item in channel.ReadAllAsync(_cancellationToken).ConfigureAwait(false))
      {
        Deserialize(item);

        long counterValue = Interlocked.Increment(ref counter);
        deserializeProgress.Report(
          new(
            $"Deserializing objects {counterValue:N0}/{estimatedObjectCount:N0}",
            (double)counterValue / estimatedObjectCount
          )
        );
      }
    }
  }

  public Base MaterializeGraph()
  {
    _cancellationToken.ThrowIfCancellationRequested();

    Parallel.ForEach(
      _packFileManager.GetObjects(_cancellationToken),
      _deserializeParallelismOptions,
      (item) =>
      {
        _cancellationToken.ThrowIfCancellationRequested();

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
