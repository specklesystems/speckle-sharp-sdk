using System.Collections.Concurrent;
using System.Diagnostics;
using Speckle.Sdk.Common;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Serialisation.Utilities;

namespace Speckle.Sdk.Transports.ServerUtils;

internal enum ServerApiOperation
{
  NoOp = default,
  DownloadSingleObject,
  DownloadObjects,
  HasObjects,
  UploadObjects,
  UploadBlobs,
  DownloadBlobs,
  HasBlobs,
}

internal class ParallelServerApi : ParallelOperationExecutor<ServerApiOperation>, IServerApi
{
  private readonly string _authToken;

  private readonly ISpeckleHttp _http;
  private readonly ISdkActivityFactory _activityFactory;
  private readonly Uri _baseUri;

  private readonly int _timeoutSeconds;

  public ParallelServerApi(
    ISpeckleHttp http,
    ISdkActivityFactory activityFactory,
    Uri baseUri,
    string authorizationToken,
    string blobStorageFolder,
    int timeoutSeconds,
    int numThreads = 4,
    int numBufferedOperations = 8
  )
  {
    _http = http;
    _activityFactory = activityFactory;
    _baseUri = baseUri;
    _authToken = authorizationToken;
    _timeoutSeconds = timeoutSeconds;
    NumThreads = numThreads;

    BlobStorageFolder = blobStorageFolder;

    NumThreads = numThreads;
    Tasks = new BlockingCollection<OperationTask<ServerApiOperation>>(numBufferedOperations);
  }

  public CancellationToken CancellationToken { get; set; }
  public bool CompressPayloads { get; set; } = true;

  public string BlobStorageFolder { get; set; }

  #region Operations

  public async Task<Dictionary<string, bool>> HasObjects(string streamId, IReadOnlyList<string> objectIds)
  {
    EnsureStarted();
    List<Task<object?>> tasks = new();
    IReadOnlyList<IReadOnlyList<string>> splitObjectsIds;
    if (objectIds.Count <= 50)
    {
      splitObjectsIds = new List<IReadOnlyList<string>> { objectIds };
    }
    else
    {
      splitObjectsIds = SplitList(objectIds, NumThreads);
    }

    for (int i = 0; i < NumThreads; i++)
    {
      if (splitObjectsIds.Count <= i || splitObjectsIds[i].Count == 0)
      {
        continue;
      }

      var op = QueueOperation(ServerApiOperation.HasObjects, (streamId, splitObjectsIds[i]));
      tasks.Add(op);
    }
    Dictionary<string, bool> ret = new();
    foreach (var task in tasks)
    {
      var taskResult = (IReadOnlyDictionary<string, bool>?)(await task.ConfigureAwait(false));
      foreach (KeyValuePair<string, bool> kv in taskResult.Empty())
      {
        ret[kv.Key] = kv.Value;
      }
    }

    return ret;
  }

  public async Task<string?> DownloadSingleObject(string streamId, string objectId, Action<ProgressArgs>? progress)
  {
    EnsureStarted();
    Task<object?> op = QueueOperation(ServerApiOperation.DownloadSingleObject, (streamId, objectId, progress));
    object? result = await op.ConfigureAwait(false);
    return (string?)result;
  }

  public async Task DownloadObjects(
    string streamId,
    IReadOnlyList<string> objectIds,
    Action<ProgressArgs>? progress,
    CbObjectDownloaded onObjectCallback
  )
  {
    EnsureStarted();
    List<Task<object?>> tasks = new();
    IReadOnlyList<IReadOnlyList<string>> splitObjectsIds = SplitList(objectIds, NumThreads);
    object callbackLock = new();

    CbObjectDownloaded callbackWrapper = (id, json) =>
    {
      lock (callbackLock)
      {
        onObjectCallback(id, json);
      }
    };

    for (int i = 0; i < NumThreads; i++)
    {
      if (splitObjectsIds[i].Count == 0)
      {
        continue;
      }

      Task<object?> op = QueueOperation(
        ServerApiOperation.DownloadObjects,
        (streamId, splitObjectsIds[i], progress, callbackWrapper)
      );
      tasks.Add(op);
    }
    await Task.WhenAll(tasks.ToArray()).ConfigureAwait(false);
  }

  public async Task UploadObjects(
    string streamId,
    IReadOnlyList<(string, string)> objects,
    Action<ProgressArgs>? progress
  )
  {
    EnsureStarted();
    List<Task<object?>> tasks = new();
    IReadOnlyList<IReadOnlyList<(string, string)>> splitObjects;

    // request count optimization: if objects are < 500k, send in 1 request
    int totalSize = 0;
    foreach ((_, string json) in objects)
    {
      totalSize += json.Length;
      if (totalSize >= 500_000)
      {
        break;
      }
    }
    splitObjects =
      totalSize >= 500_000 ? SplitList(objects, NumThreads) : new List<IReadOnlyList<(string, string)>> { objects };

    for (int i = 0; i < NumThreads; i++)
    {
      if (splitObjects.Count <= i || splitObjects[i].Count == 0)
      {
        continue;
      }

      var op = QueueOperation(ServerApiOperation.UploadObjects, (streamId, splitObjects[i], progress));
      tasks.Add(op);
    }
    await Task.WhenAll(tasks.ToArray()).ConfigureAwait(false);
  }

  public async Task UploadBlobs(string streamId, IReadOnlyList<(string, string)> blobs, Action<ProgressArgs>? progress)
  {
    EnsureStarted();
    var op = QueueOperation(ServerApiOperation.UploadBlobs, (streamId, blobs, progress));
    await op.ConfigureAwait(false);
  }

  public async Task DownloadBlobs(string streamId, IReadOnlyList<string> blobIds, Action<ProgressArgs>? progress)
  {
    EnsureStarted();
    var op = QueueOperation(ServerApiOperation.DownloadBlobs, (streamId, blobIds, progress));
    await op.ConfigureAwait(false);
  }

  public async Task<List<string>> HasBlobs(string streamId, IReadOnlyList<(string, string)> blobs)
  {
    EnsureStarted();
    Task<object?> op = QueueOperation(ServerApiOperation.HasBlobs, (streamId, blobs));
    var res = (List<string>?)await op.ConfigureAwait(false);
    Debug.Assert(res is not null);
    return res.NotNull();
  }

  #endregion

  public void EnsureStarted()
  {
    if (Threads.Count == 0)
    {
      Start();
    }
  }

  protected override void ThreadMain()
  {
    using ServerApi serialApi = new(_http, _activityFactory, _baseUri, _authToken, BlobStorageFolder, _timeoutSeconds);
    serialApi.CancellationToken = CancellationToken;
    serialApi.CompressPayloads = CompressPayloads;

    while (true)
    {
      if (IsDisposed)
      {
        return;
      }

      var (operation, inputValue, tcs) = Tasks.Take();

      if (operation == ServerApiOperation.NoOp || tcs == null)
      {
        return;
      }

      try
      {
        var result = RunOperation(operation, inputValue.NotNull(), serialApi).GetAwaiter().GetResult();
        tcs.SetResult(result);
      }
      catch (Exception ex)
      {
        tcs.SetException(ex);

        if (ex.IsFatal())
        {
          throw;
        }
      }
    }
  }

  private static async Task<object?> RunOperation(ServerApiOperation operation, object inputValue, ServerApi serialApi)
  {
    switch (operation)
    {
      case ServerApiOperation.DownloadSingleObject:
        var (dsoStreamId, dsoObjectId, progress) = ((string, string, Action<ProgressArgs>?))inputValue;
        return await serialApi.DownloadSingleObject(dsoStreamId, dsoObjectId, progress).ConfigureAwait(false);
      case ServerApiOperation.DownloadObjects:
        var (doStreamId, doObjectIds, progress2, doCallback) = ((
          string,
          IReadOnlyList<string>,
          Action<ProgressArgs>?,
          CbObjectDownloaded
        ))inputValue;
        await serialApi.DownloadObjects(doStreamId, doObjectIds, progress2, doCallback).ConfigureAwait(false);
        return null;
      case ServerApiOperation.HasObjects:
        var (hoStreamId, hoObjectIds) = ((string, IReadOnlyList<string>))inputValue;
        return await serialApi.HasObjects(hoStreamId, hoObjectIds).ConfigureAwait(false);
      case ServerApiOperation.UploadObjects:
        var (uoStreamId, uoObjects, progress3) = ((
          string,
          IReadOnlyList<(string, string)>,
          Action<ProgressArgs>?
        ))inputValue;
        await serialApi.UploadObjects(uoStreamId, uoObjects, progress3).ConfigureAwait(false);
        return null;
      case ServerApiOperation.UploadBlobs:
        var (ubStreamId, ubBlobs, progress4) = ((
          string,
          IReadOnlyList<(string, string)>,
          Action<ProgressArgs>?
        ))inputValue;
        await serialApi.UploadBlobs(ubStreamId, ubBlobs, progress4).ConfigureAwait(false);
        return null;
      case ServerApiOperation.HasBlobs:
        var (hbStreamId, hBlobs) = ((string, IReadOnlyList<(string, string)>))inputValue;
        return await serialApi
          .HasBlobs(hbStreamId, hBlobs.Select(b => b.Item1.Split(':')[1]).ToList())
          .ConfigureAwait(false);
      case ServerApiOperation.DownloadBlobs:
        var (dbStreamId, blobIds, progress5) = ((string, IReadOnlyList<string>, Action<ProgressArgs>?))inputValue;
        await serialApi.DownloadBlobs(dbStreamId, blobIds, progress5).ConfigureAwait(false);
        return null;
      default:
        throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
    }
  }

  private Task<object?> QueueOperation(ServerApiOperation operation, object? inputValue)
  {
    TaskCompletionSource<object?> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    Tasks.Add(new(operation, inputValue, tcs));
    return tcs.Task;
  }

  private static List<List<T>> SplitList<T>(IReadOnlyList<T> list, int parts)
  {
    List<List<T>> ret = new(parts);
    for (int i = 0; i < parts; i++)
    {
      ret.Add(new List<T>(list.Count / parts + 1));
    }

    for (int i = 0; i < list.Count; i++)
    {
      ret[i % parts].Add(list[i]);
    }

    return ret;
  }
}
