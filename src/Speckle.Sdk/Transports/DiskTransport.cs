using System.Diagnostics;
using System.Text;
using Speckle.Sdk.Logging;

namespace Speckle.Sdk.Transports;

/// <summary>
/// Writes speckle objects to disk.
/// </summary>
public class DiskTransport : ICloneable, ITransport
{
  public DiskTransport(string? basePath = null)
  {
    basePath ??= Path.Combine(SpecklePathProvider.UserSpeckleFolderPath, "DiskTransportFiles");

    RootPath = Path.Combine(basePath);

    Directory.CreateDirectory(RootPath);
  }

  public string RootPath { get; set; }

  public object Clone()
  {
    return new DiskTransport
    {
      RootPath = RootPath,
      CancellationToken = CancellationToken,
      OnErrorAction = OnErrorAction,
      OnProgressAction = OnProgressAction,
      TransportName = TransportName
    };
  }

  public string TransportName { get; set; } = "Disk";

  public Dictionary<string, object> TransportContext =>
    new()
    {
      { "name", TransportName },
      { "type", GetType().Name },
      { "basePath", RootPath }
    };

  public CancellationToken CancellationToken { get; set; }

  public Action<ProgressArgs>? OnProgressAction { get; set; }

  public Action<string, Exception>? OnErrorAction { get; set; }

  public int SavedObjectCount { get; private set; }

  public TimeSpan Elapsed { get; set; } = TimeSpan.Zero;

  public void BeginWrite()
  {
    SavedObjectCount = 0;
  }

  public void EndWrite() { }

  public ValueTask<string?> GetObject(string id)
  {
    CancellationToken.ThrowIfCancellationRequested();

    var filePath = Path.Combine(RootPath, id);
    if (File.Exists(filePath))
    {
#if NETSTANDARD2_0
      return new ValueTask<string?>(File.ReadAllText(filePath, Encoding.UTF8));
#else
    return ValueTask.FromResult<string?>(File.ReadAllText(filePath, Encoding.UTF8));
#endif
    }
#if NETSTANDARD2_0
    return new ValueTask<string?>((string?)null);
#else
    return ValueTask.FromResult<string?>(null);
#endif
  }

  public void SaveObject(string id, string serializedObject)
  {
    var stopwatch = Stopwatch.StartNew();
    CancellationToken.ThrowIfCancellationRequested();

    var filePath = Path.Combine(RootPath, id);
    if (File.Exists(filePath))
    {
      return;
    }

    try
    {
      File.WriteAllText(filePath, serializedObject, Encoding.UTF8);
    }
    catch (Exception ex)
    {
      throw new TransportException(this, $"Failed to write object {id} to disk", ex);
    }

    SavedObjectCount++;
    OnProgressAction?.Invoke(new(ProgressEvent.DownloadObject, SavedObjectCount, null));
    stopwatch.Stop();
    Elapsed += stopwatch.Elapsed;
  }

  public ValueTask WriteComplete()
  {
#if NETSTANDARD2_0
    return new ValueTask(Task.CompletedTask);
#else
    return ValueTask.CompletedTask;
#endif
  }

  public async ValueTask<string> CopyObjectAndChildren(
    string id,
    ITransport targetTransport,
    Action<int>? onTotalChildrenCountKnown = null
  )
  {
    string res = await TransportHelpers
      .CopyObjectAndChildrenAsync(id, this, targetTransport, onTotalChildrenCountKnown, CancellationToken)
      .ConfigureAwait(false);
    return res;
  }

  public ValueTask<Dictionary<string, bool>> HasObjects(IReadOnlyList<string> objectIds)
  {
    Dictionary<string, bool> ret = new();
    foreach (string objectId in objectIds)
    {
      var filePath = Path.Combine(RootPath, objectId);
      ret[objectId] = File.Exists(filePath);
    }
#if NETSTANDARD2_0
    return new ValueTask<Dictionary<string, bool>>(ret);
#else
    return ValueTask.FromResult(ret);
#endif
  }

  public override string ToString()
  {
    return $"Disk Transport @{RootPath}";
  }
}
