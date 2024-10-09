using System.Collections.Concurrent;
using System.Diagnostics;
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
  public IReadOnlyDictionary<string, string> Objects => _objects;
  private readonly ConcurrentDictionary<string, string> _objects;

  public MemoryTransport(
    ConcurrentDictionary<string, string>? objects = null,
    bool blobStorageEnabled = false,
    string? basePath = null,
    string? applicationName = null
  )
  {
    _objects = objects ?? new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
    _blobStorageEnabled = blobStorageEnabled;
    _basePath = basePath ?? SpecklePathProvider.UserApplicationDataPath();
    _applicationName = applicationName ?? "Speckle";
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
    return new MemoryTransport(_objects, _blobStorageEnabled, _basePath, _applicationName)
    {
      TransportName = TransportName,
      OnProgressAction = OnProgressAction,
      CancellationToken = CancellationToken,
      SavedObjectCount = SavedObjectCount,
    };
  }

  public CancellationToken CancellationToken { get; set; }

  public string TransportName { get; set; } = "Memory";

  public IProgress<ProgressArgs>? OnProgressAction { get; set; }

  public int SavedObjectCount { get; private set; }

  public Dictionary<string, object> TransportContext =>
    new()
    {
      { "name", TransportName },
      { "type", GetType().Name },
      { "basePath", _basePath },
      { "applicationName", _applicationName },
      { "blobStorageFolder", BlobStorageFolder },
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

    _objects[id] = serializedObject;

    SavedObjectCount++;
    OnProgressAction?.Report(new(ProgressEvent.UploadObject, 1, 1));
    stopwatch.Stop();
    Elapsed += stopwatch.Elapsed;
  }

  public Task<string?> GetObject(string id)
  {
    var stopwatch = Stopwatch.StartNew();
    var ret = Objects.TryGetValue(id, out string? o) ? o : null;
    stopwatch.Stop();
    Elapsed += stopwatch.Elapsed;
    return Task.FromResult(ret);
  }

  public async Task<string> CopyObjectAndChildren(string id, ITransport targetTransport)
  {
    string res = await TransportHelpers
      .CopyObjectAndChildrenAsync(id, this, targetTransport, CancellationToken)
      .ConfigureAwait(false);
    return res;
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
