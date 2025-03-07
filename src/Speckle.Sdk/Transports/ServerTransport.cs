using System.Diagnostics;
using Speckle.Sdk.Common;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.Utilities;
using Speckle.Sdk.Transports.ServerUtils;

namespace Speckle.Sdk.Transports;

public sealed class ServerTransport : IServerTransport
{
  private readonly ISpeckleHttp _http;
  private readonly ISdkActivityFactory _activityFactory;
  private readonly object _elapsedLock = new();

  private Exception? _exception;
  private bool IsInErrorState => _exception is not null;
  private bool _isWriteComplete;

  // TODO: make send buffer more flexible to accept blobs too
  private List<(string id, string data)> _sendBuffer = new();
  private readonly object _sendBufferLock = new();
  private Thread? _sendingThread;

  private volatile bool _shouldSendThreadRun;

  /// <param name="account"></param>
  /// <param name="streamId"></param>
  /// <param name="timeoutSeconds"></param>
  /// <param name="blobStorageFolder">Defaults to <see cref="SpecklePathProvider.BlobStoragePath"/></param>
  /// <exception cref="ArgumentException"><paramref name="streamId"/> was not formatted as valid stream id</exception>
  public ServerTransport(
    ISpeckleHttp http,
    ISdkActivityFactory activityFactory,
    Account account,
    string streamId,
    int timeoutSeconds = 60,
    string? blobStorageFolder = null
  )
  {
    if (string.IsNullOrWhiteSpace(streamId))
    {
      throw new ArgumentException($"{streamId} is not a valid id", streamId);
    }

    _http = http;
    _activityFactory = activityFactory;

    Account = account;
    BaseUri = new(account.serverInfo.url);
    StreamId = streamId;
    AuthorizationToken = account.token;
    TimeoutSeconds = timeoutSeconds;
    BlobStorageFolder = blobStorageFolder ?? SpecklePathProvider.BlobStoragePath();
    Api = new ParallelServerApi(http, activityFactory, BaseUri, AuthorizationToken, BlobStorageFolder, TimeoutSeconds);

    Directory.CreateDirectory(BlobStorageFolder);
  }

  public Account Account { get; }
  public Uri BaseUri { get; }
  public string StreamId { get; internal set; }

  public int TimeoutSeconds { get; set; }
  private string AuthorizationToken { get; }

  internal ParallelServerApi Api { get; private set; }

  public string BlobStorageFolder { get; set; }

  public void SaveBlob(Blob obj)
  {
    var hash = obj.GetFileHash();

    lock (_sendBufferLock)
    {
      if (IsInErrorState)
      {
        throw new TransportException("Server transport is in an errored state", _exception);
      }

      _sendBuffer.Add(($"blob:{hash}", obj.filePath));
    }
  }

  public object Clone()
  {
    return new ServerTransport(_http, _activityFactory, Account, StreamId, TimeoutSeconds, BlobStorageFolder)
    {
      OnProgressAction = OnProgressAction,
      CancellationToken = CancellationToken,
    };
  }

  public void Dispose()
  {
    if (_sendingThread != null)
    {
      _shouldSendThreadRun = false;
      _sendingThread.Join();
    }
    Api.Dispose();
  }

  public string TransportName { get; set; } = "RemoteTransport";

  public Dictionary<string, object> TransportContext =>
    new()
    {
      { "name", TransportName },
      { "type", GetType().Name },
      { "streamId", StreamId },
      { "serverUrl", BaseUri },
      { "blobStorageFolder", BlobStorageFolder },
    };

  public CancellationToken CancellationToken { get; set; }
  public IProgress<ProgressArgs>? OnProgressAction { get; set; }
  public TimeSpan Elapsed { get; private set; } = TimeSpan.Zero;

  public async Task<string> CopyObjectAndChildren(string id, ITransport targetTransport)
  {
    if (string.IsNullOrEmpty(id))
    {
      throw new ArgumentException("Cannot copy object with empty id", nameof(id));
    }

    CancellationToken.ThrowIfCancellationRequested();

    using ParallelServerApi api = new(
      _http,
      _activityFactory,
      BaseUri,
      AuthorizationToken,
      BlobStorageFolder,
      TimeoutSeconds
    );

    var stopwatch = Stopwatch.StartNew();
    api.CancellationToken = CancellationToken;

    string? rootObjectJson = await api.DownloadSingleObject(StreamId, id, OnProgressAction).ConfigureAwait(false);
    var allIds = ClosureParser.GetChildrenIds(rootObjectJson.NotNull(), CancellationToken).ToList();

    var childrenIds = allIds.Where(x => !x.Contains("blob:"));
    var blobIds = allIds.Where(x => x.Contains("blob:")).Select(x => x.Remove(0, 5));

    //
    // Objects download
    //

    // Check which children are not already in the local transport
    var childrenFoundMap = await targetTransport.HasObjects(childrenIds.ToList()).ConfigureAwait(false);
    List<string> newChildrenIds = new(from objId in childrenFoundMap.Keys where !childrenFoundMap[objId] select objId);

    targetTransport.BeginWrite();

    await api.DownloadObjects(
        StreamId,
        newChildrenIds,
        OnProgressAction,
        (childId, childData) =>
        {
          stopwatch.Stop();
          targetTransport.SaveObject(childId, childData);
          stopwatch.Start();
        }
      )
      .ConfigureAwait(false);

    // pausing until writing to the target transport
    stopwatch.Stop();
    targetTransport.SaveObject(id, rootObjectJson);

    await targetTransport.WriteComplete().ConfigureAwait(false);
    targetTransport.EndWrite();
    stopwatch.Start();

    //
    // Blobs download
    //
    var localBlobTrimmedHashes = Directory
      .GetFiles(BlobStorageFolder)
      .Select(fileName => fileName.Split(Path.DirectorySeparatorChar).Last())
      .Where(fileName => fileName.Length > 10)
      .Select(fileName => fileName[..Blob.LocalHashPrefixLength])
      .ToList();

    var newBlobIds = blobIds
      .Where(blobId => !localBlobTrimmedHashes.Contains(blobId[..Blob.LocalHashPrefixLength]))
      .ToList();

    await api.DownloadBlobs(StreamId, newBlobIds, OnProgressAction).ConfigureAwait(false);

    stopwatch.Stop();
    Elapsed += stopwatch.Elapsed;
    return rootObjectJson;
  }

  public async Task<string?> GetObject(string id)
  {
    CancellationToken.ThrowIfCancellationRequested();
    var stopwatch = Stopwatch.StartNew();
    var result = await Api.DownloadSingleObject(StreamId, id, OnProgressAction).ConfigureAwait(false);
    stopwatch.Stop();
    Elapsed += stopwatch.Elapsed;
    return result;
  }

  public async Task<Dictionary<string, bool>> HasObjects(IReadOnlyList<string> objectIds)
  {
    return await Api.HasObjects(StreamId, objectIds).ConfigureAwait(false);
  }

  public void SaveObject(string id, string serializedObject)
  {
    lock (_sendBufferLock)
    {
      if (IsInErrorState)
      {
        throw new TransportException($"{TransportName} transport failed", _exception);
      }

      _sendBuffer.Add((id, serializedObject));
      _isWriteComplete = false;
    }
  }

  public void BeginWrite()
  {
    if (_shouldSendThreadRun || _sendingThread != null)
    {
      throw new InvalidOperationException("ServerTransport already sending");
    }

    _exception = null;
    _shouldSendThreadRun = true;
    _sendingThread = new Thread(SendingThreadMain) { Name = "ServerTransportSender", IsBackground = true };
    _sendingThread.Start();
  }

  public async Task WriteComplete()
  {
    while (true)
    {
      lock (_sendBufferLock)
      {
        if (_isWriteComplete || IsInErrorState)
        {
          CancellationToken.ThrowIfCancellationRequested();

          if (_exception is not null)
          {
            throw new TransportException(this, $"{TransportName} transport failed", _exception);
          }

          return;
        }
      }

      await Task.Delay(50, CancellationToken).ConfigureAwait(false);
    }
  }

  public void EndWrite()
  {
    if (!_shouldSendThreadRun || _sendingThread == null)
    {
      throw new InvalidOperationException("ServerTransport not sending");
    }

    _shouldSendThreadRun = false;
    _sendingThread.Join();
    _sendingThread = null;
  }

  public override string ToString()
  {
    return $"Server Transport @{Account.serverInfo.url}";
  }

  private async void SendingThreadMain()
  {
    while (true)
    {
      var stopwatch = Stopwatch.StartNew();
      if (!_shouldSendThreadRun || CancellationToken.IsCancellationRequested)
      {
        return;
      }

      List<(string id, string data)>? buffer = null;
      lock (_sendBufferLock)
      {
        if (_sendBuffer.Count > 0)
        {
          buffer = _sendBuffer;
          _sendBuffer = new();
        }
        else
        {
          _isWriteComplete = true;
        }
      }

      if (buffer is null)
      {
        Thread.Sleep(100);
        continue;
      }
      try
      {
        var bufferObjects = buffer.Where(tuple => !tuple.id.Contains("blob")).ToList();
        var bufferBlobs = buffer.Where(tuple => tuple.id.Contains("blob")).ToList();

        List<string> objectIds = new(bufferObjects.Count);

        foreach ((string id, _) in bufferObjects)
        {
          if (id != "blob")
          {
            objectIds.Add(id);
          }
        }

        Dictionary<string, bool> hasObjects = await Api.HasObjects(StreamId, objectIds).ConfigureAwait(false);
        List<(string, string)> newObjects = new();
        foreach ((string id, object json) in bufferObjects)
        {
          if (!hasObjects[id])
          {
            newObjects.Add((id, (string)json));
          }
        }

        await Api.UploadObjects(StreamId, newObjects, OnProgressAction).ConfigureAwait(false);

        if (bufferBlobs.Count != 0)
        {
          var blobIdsToUpload = await Api.HasBlobs(StreamId, bufferBlobs).ConfigureAwait(false);
          var formattedIds = blobIdsToUpload.Select(id => $"blob:{id}").ToList();
          var newBlobs = bufferBlobs.Where(tuple => formattedIds.IndexOf(tuple.id) != -1).ToList();
          if (newBlobs.Count != 0)
          {
            await Api.UploadBlobs(StreamId, newBlobs, OnProgressAction).ConfigureAwait(false);
          }
        }
      }
      catch (Exception ex)
      {
        lock (_sendBufferLock)
        {
          _sendBuffer.Clear();
          _exception = ex;
        }

        if (ex.IsFatal())
        {
          throw;
        }
      }
      finally
      {
        stopwatch.Stop();
        lock (_elapsedLock)
        {
          Elapsed += stopwatch.Elapsed;
        }
      }
    }
  }
}
