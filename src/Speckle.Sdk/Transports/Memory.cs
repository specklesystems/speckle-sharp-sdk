using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Transports;

/// <summary>
/// An in memory storage of speckle objects.
/// </summary>
public sealed class MemoryTransport : ITransport, ICloneable, IBlobCapableTransport
{
  private readonly string _basePath;
  private readonly string _applicationName;
  private readonly bool _blobStorageEnabled;
  public IDictionary<string, string> Objects { get; }

  public MemoryTransport()
    : this(new Dictionary<string, string>()) { }

  public MemoryTransport(
    IDictionary<string, string> objects,
    bool blobStorageEnabled = false,
    string? basePath = null,
    string? applicationName = null
  )
  {
    Objects = objects;
    _blobStorageEnabled = blobStorageEnabled;
    _basePath = basePath ?? SpecklePathProvider.UserApplicationDataPath();
    _applicationName = applicationName ?? "Speckle";
    SpeckleLog.Logger.Debug("Creating a new Memory Transport");
    var dir = Path.Combine(_basePath, _applicationName);
    try
    {
      Directory.CreateDirectory(dir); //ensure dir is there
    }
    catch (Exception ex)
      when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
    {
      throw new TransportException($"Path was invalid or could not be created {dir}", ex);
    }
  }

  public object Clone()
  {
    return new MemoryTransport(Objects, _blobStorageEnabled, _basePath, _applicationName)
    {
      TransportName = TransportName,
      OnProgressAction = OnProgressAction,
      CancellationToken = CancellationToken,
      SavedObjectCount = SavedObjectCount
    };
  }

  public CancellationToken CancellationToken { get; set; }

  public string TransportName { get; set; } = "Memory";

  public Action<ProgressArgs>? OnProgressAction { get; set; }

  public int SavedObjectCount { get; private set; }

  public Dictionary<string, object> TransportContext =>
    new()
    {
      { "name", TransportName },
      { "type", GetType().Name },
      { "basePath", _basePath },
      { "applicationName", _applicationName },
      { "blobStorageFolder", BlobStorageFolder }
    };

  public TimeSpan Elapsed { get; private set; } = TimeSpan.Zero;

  public void BeginWrite()
  {
    SavedObjectCount = 0;
  }

  public void EndWrite() { }

  public void SaveObject(string id, string serializedObject)
  {
    CancellationToken.ThrowIfCancellationRequested();
    var stopwatch = Stopwatch.StartNew();

    Objects[id] = serializedObject;

    SavedObjectCount++;
    OnProgressAction?.Invoke(new(ProgressEvent.UploadObject, 1, 1));
    stopwatch.Stop();
    Elapsed += stopwatch.Elapsed;
  }

  public string? GetObject(string id)
  {
    var stopwatch = Stopwatch.StartNew();
    var ret = Objects.TryGetValue(id, out string? o) ? o : null;
    stopwatch.Stop();
    Elapsed += stopwatch.Elapsed;
    return ret;
  }

  public Task<string> CopyObjectAndChildren(
    string id,
    ITransport targetTransport,
    Action<int>? onTotalChildrenCountKnown = null
  )
  {
    string res = TransportHelpers.CopyObjectAndChildrenSync(
      id,
      this,
      targetTransport,
      onTotalChildrenCountKnown,
      CancellationToken
    );
    return Task.FromResult(res);
  }

  public Task WriteComplete()
  {
    return Task.CompletedTask;
  }

  public Task<Dictionary<string, bool>> HasObjects(IReadOnlyList<string> objectIds)
  {
    Dictionary<string, bool> ret = new(objectIds.Count);
    foreach (string objectId in objectIds)
    {
      ret[objectId] = Objects.ContainsKey(objectId);
    }

    return Task.FromResult(ret);
  }

  public override string ToString()
  {
    return $"Memory Transport {TransportName}";
  }

  public string BlobStorageFolder => SpecklePathProvider.BlobStoragePath(Path.Combine(_basePath, _applicationName));

  public void SaveBlob(Blob obj)
  {
    if (!_blobStorageEnabled)
    {
      return;
    }
    var blobPath = obj.originalPath;
    var targetPath = obj.GetLocalDestinationPath(BlobStorageFolder);
    File.Copy(blobPath, targetPath, true);
  }
}
