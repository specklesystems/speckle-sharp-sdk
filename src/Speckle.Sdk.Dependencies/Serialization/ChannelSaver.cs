﻿using System.Threading.Channels;
using Open.ChannelExtensions;

namespace Speckle.Sdk.Dependencies.Serialization;

public readonly record struct BaseItem(string Id, string Json);

public abstract class ChannelSaver
{
  private const int HTTP_SEND_CHUNK_SIZE = 500;
  private const int MAX_PARALLELISM_HTTP = 4;
  private static readonly TimeSpan HTTP_BATCH_TIMEOUT = TimeSpan.FromSeconds(2);
  private static readonly int MAX_CACHE_PARALLELISM = Environment.ProcessorCount;
  private const int SQLITE_BATCH = 500;

  private readonly Channel<BaseItem> _checkCacheChannel = Channel.CreateUnbounded<BaseItem>();

  public Task Start(string streamId, string rootId, CancellationToken cancellationToken = default)
  {
    return _checkCacheChannel
      .Reader.Batch(SQLITE_BATCH)
      .WithTimeout(HTTP_BATCH_TIMEOUT)
      .Pipe(MAX_CACHE_PARALLELISM, x => CheckCache(rootId, x), -1, false, cancellationToken)
      .Join()
      .Batch(HTTP_SEND_CHUNK_SIZE)
      .WithTimeout(HTTP_BATCH_TIMEOUT)
      .PipeAsync(
        MAX_PARALLELISM_HTTP,
        async x => await SendToServer(streamId, x, cancellationToken).ConfigureAwait(false),
        -1,
        false,
        cancellationToken
      )
      .Join()
      .Batch(SQLITE_BATCH)
      .WithTimeout(HTTP_BATCH_TIMEOUT)
      .ReadAllConcurrently(MAX_CACHE_PARALLELISM, x => SaveToCache(rootId, x), cancellationToken);
  }

  public async Task Save(string id, string json, CancellationToken cancellationToken = default) =>
    await _checkCacheChannel.Writer.WriteAsync(new(id, json), cancellationToken).ConfigureAwait(false);

  public void Done() => _checkCacheChannel.Writer.TryComplete();

  public abstract List<BaseItem> CheckCache(string rootId, List<BaseItem> item);

  public abstract Task<List<BaseItem>> SendToServer(
    string streamId,
    List<BaseItem> batch,
    CancellationToken cancellationToken
  );

  public abstract void SaveToCache(string rootId, List<BaseItem> item);
}
