using System.Collections.Concurrent;
using System.Threading.Channels;
using Open.ChannelExtensions;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.Utilities;
using Speckle.Sdk.Transports;
using Speckle.Sdk.Transports.ServerUtils;

namespace Speckle.Sdk.Serialisation.Receive;

public record ReceiveProcessSettings(
  int MaxDownloadThreads = 4,
  int MaxDeserializeThreads = 4,
  int MaxObjectRequestSize = ServerApi.BATCH_SIZE_GET_OBJECTS - 1,
  int BatchWaitMilliseconds = 500
);

public sealed class ReceiveProcess : IDisposable
{
  private readonly ReceiveProcessSettings _settings = new();
  private readonly ConcurrentDictionary<string, Base> _idToBaseCache = new(StringComparer.Ordinal);

  private string? _rootObjectId;
  private Base? _rootObject;

  public ReceiveProcess(IModelSource modelSource, ReceiveProcessSettings? settings = null)
  {
    if (settings is not null)
    {
      _settings = settings;
    }
    SourceChannel = Channel.CreateUnbounded<string>();
    CachingStage = new(_idToBaseCache);
    GatherStage = new GatherStage(modelSource);
    DeserializeStage = new(_idToBaseCache, EnqueueObject);
  }

  private Channel<string> SourceChannel { get; }

  private long _bytes;
  public Action<ProgressArgs[]>? Progress { get; set; }

  public void InvokeProgress() =>
    Progress?.Invoke(
      [
        new ProgressArgs(ProgressEvent.DownloadBytes, _bytes, null),
        new ProgressArgs(ProgressEvent.DeserializeObject, DeserializeStage.Deserialized, null)
      ]
    );

  private async ValueTask EnqueueObject(string id) => await SourceChannel.Writer.WriteAsync(id).ConfigureAwait(false);

  public async ValueTask<Base> GetObject(
    string objectId,
    Action<ProgressArgs[]>? progress,
    CancellationToken cancellationToken
  )
  {
    Progress = progress;
    _rootObjectId = objectId;

    var pipelineTask = SourceChannel
      .Reader.Batch(_settings.MaxObjectRequestSize)
      .WithTimeout(TimeSpan.FromMilliseconds(_settings.BatchWaitMilliseconds))
      .PipeAsync(_settings.MaxDownloadThreads, OnTransport, cancellationToken: cancellationToken)
      .Join()
      .ReadAllConcurrentlyAsync(_settings.MaxDeserializeThreads, OnDeserialize, cancellationToken: cancellationToken)
      .ConfigureAwait(false);

    var rootJson = await GatherStage
      .DownloadRoot(
        objectId,
        args =>
        {
          _bytes += args.Count ?? 0;
          InvokeProgress();
        }
      )
      .ConfigureAwait(false);

    var closures = (await ClosureParser.GetChildrenIdsAsync(rootJson, cancellationToken).ConfigureAwait(false));
    foreach (var closure in closures)
    {
      await EnqueueObject(closure).ConfigureAwait(false);
    }
    await EnqueueObject(_rootObjectId).ConfigureAwait(false);
    var total = await pipelineTask;
    Console.WriteLine($"Total {_idToBaseCache.Count}");
    return _rootObject.NotNull();
  }

  private async ValueTask<List<Downloaded>> OnTransport(List<string> batch)
  {
    var gathered = GatherStage
      .Execute(
        batch,
        args =>
        {
          _bytes += args.Count ?? 0;
          InvokeProgress();
        }
      )
      .ConfigureAwait(false);
    var ret = new List<Downloaded>();
    await foreach (var arg in gathered)
    {
      if (!_idToBaseCache.ContainsKey(arg.Id))
      {
        ret.Add(arg);
      }
    }
    InvokeProgress();
    return ret;
  }

  private async ValueTask OnDeserialize(Downloaded transported)
  {
    var deserialized = await DeserializeStage.Execute(transported).ConfigureAwait(false);
    if (deserialized is null)
    {
      return;
    }
    InvokeProgress();
    _idToBaseCache.TryAdd(deserialized.Id, deserialized.BaseObject);
    if (deserialized.Id == _rootObjectId)
    {
      _rootObject = deserialized.BaseObject;
      SourceChannel.Writer.Complete();
    }
  }

  public CachingStage CachingStage { get; }
  public GatherStage GatherStage { get; }
  public DeserializeStage DeserializeStage { get; }

  public void Dispose() => GatherStage.Dispose();
}
