
using System.Diagnostics;
using System.Threading.Channels;
using Open.ChannelExtensions;
using Speckle.Sdk.Common;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.Utilities;
using Speckle.Sdk.Transports.ServerUtils;

namespace Speckle.Sdk.Transports;

public sealed class ServerTransport2 : IBlobCapableTransport
{
  private readonly ISpeckleHttp _http;
  private readonly ISdkActivityFactory _activityFactory;


  // TODO: make send buffer more flexible to accept blobs too
  private readonly Channel<(string id, string data)> _sourceChannel = Channel.CreateUnbounded<(string id, string data)>();
  /// <param name="account"></param>
  /// <param name="streamId"></param>
  /// <param name="timeoutSeconds"></param>
  /// <param name="blobStorageFolder">Defaults to <see cref="SpecklePathProvider.BlobStoragePath"/></param>
  /// <exception cref="ArgumentException"><paramref name="streamId"/> was not formatted as valid stream id</exception>
  public ServerTransport2(
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
    Api = new ServerApi(http, activityFactory, BaseUri, AuthorizationToken, BlobStorageFolder, TimeoutSeconds);

    Directory.CreateDirectory(BlobStorageFolder);
  }

  public Account Account { get; }
  public Uri BaseUri { get; }
  public string StreamId { get; internal set; }

  public int TimeoutSeconds { get; set; }
  private string AuthorizationToken { get; }

  internal ServerApi Api { get; private set; }

  public string BlobStorageFolder { get; set; }

  public async ValueTask Start()
  {
    await _sourceChannel.Reader.Batch(1000)
      .WithTimeout(TimeSpan.FromMilliseconds(100))
      .ReadAllAsync(Send, CancellationToken)
      .ConfigureAwait(false);
  }

  public async Task SaveBlob(Blob obj)
  {
    var hash = obj.GetFileHash();
    await _sourceChannel.Writer.WriteAsync(($"blob:{hash}", obj.filePath), CancellationToken).ConfigureAwait(false);
  }

  public object Clone()
  {
    return new ServerTransport(_http, _activityFactory, Account, StreamId, TimeoutSeconds, BlobStorageFolder)
    {
      OnProgressAction = OnProgressAction,
      CancellationToken = CancellationToken
    };
  }

  public void Dispose()
  {
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
      { "blobStorageFolder", BlobStorageFolder }
    };

  public CancellationToken CancellationToken { get; set; }
  public Action<ProgressArgs>? OnProgressAction { get; set; }
  public TimeSpan Elapsed { get; private set; } = TimeSpan.Zero;

  public async Task<string> CopyObjectAndChildren(
    string id,
    ITransport targetTransport,
    Action<int>? onTotalChildrenCountKnown = null
  )
  {
    if (string.IsNullOrEmpty(id))
    {
      throw new ArgumentException("Cannot copy object with empty id", nameof(id));
    }

    CancellationToken.ThrowIfCancellationRequested();


    var stopwatch = Stopwatch.StartNew();

    string? rootObjectJson = await Api.DownloadSingleObject(StreamId, id, OnProgressAction).ConfigureAwait(false);
    var allIds = (
      await ClosureParser.GetChildrenIdsAsync(rootObjectJson.NotNull(), CancellationToken).ConfigureAwait(false)
    ).ToList();

    var childrenIds = allIds.Where(x => !x.Contains("blob:"));
    var blobIds = allIds.Where(x => x.Contains("blob:")).Select(x => x.Remove(0, 5));

    onTotalChildrenCountKnown?.Invoke(allIds.Count);

    //
    // Objects download
    //

    // Check which children are not already in the local transport
    var childrenFoundMap = await targetTransport.HasObjects(childrenIds.ToList()).ConfigureAwait(false);
    List<string> newChildrenIds = new(from objId in childrenFoundMap.Keys where !childrenFoundMap[objId] select objId);

    targetTransport.BeginWrite();

    await Api.DownloadObjects(
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

    await Api.DownloadBlobs(StreamId, newBlobIds, OnProgressAction).ConfigureAwait(false);

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

  public async ValueTask SaveObject(string id, string serializedObject)
  {
      await _sourceChannel.Writer.WriteAsync((id, serializedObject)).ConfigureAwait(false);
  }

  public override string ToString()
  {
    return $"Server Transport @{Account.serverInfo.url}";
  }

  private async ValueTask Send(List<(string id, string data)> buffer)
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
      var blobIdsToUpload = await Api.HasBlobs(StreamId, bufferBlobs.Select(x => x.id).ToList()).ConfigureAwait(false);
      var formattedIds = blobIdsToUpload.Select(id => $"blob:{id}").ToList();
      var newBlobs = bufferBlobs.Where(tuple => formattedIds.IndexOf(tuple.id) != -1).ToList();
      if (newBlobs.Count != 0)
      {
        await Api.UploadBlobs(StreamId, newBlobs, OnProgressAction).ConfigureAwait(false);
      }
    }
  }
}
