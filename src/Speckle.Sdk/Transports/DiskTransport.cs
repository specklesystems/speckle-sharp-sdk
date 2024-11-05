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
      TransportName = TransportName,
    };
  }

  public string TransportName { get; set; } = "Disk";

  public Dictionary<string, object> TransportContext =>
    new()
    {
      { "name", TransportName },
      { "type", GetType().Name },
      { "basePath", RootPath },
    };

  public CancellationToken CancellationToken { get; set; }

  public IProgress<ProgressArgs>? OnProgressAction { get; set; }

  public Action<string, Exception>? OnErrorAction { get; set; }

  public int SavedObjectCount { get; private set; }

  public TimeSpan Elapsed { get; set; } = TimeSpan.Zero;

  public void BeginWrite()
  {
    SavedObjectCount = 0;
  }

  public void EndWrite() { }

  public Task<string?> GetObject(string id)
  {
    CancellationToken.ThrowIfCancellationRequested();

    var filePath = Path.Combine(RootPath, id);
    if (File.Exists(filePath))
    {
      return Task.FromResult<string?>(File.ReadAllText(filePath, Encoding.UTF8));
    }

    return Task.FromResult<string?>(null);
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
    stopwatch.Stop();
    Elapsed += stopwatch.Elapsed;
  }

  public Task WriteComplete()
  {
    return Task.CompletedTask;
  }

  public async Task<string> CopyObjectAndChildren(string id, ITransport targetTransport)
  {
    string res = await TransportHelpers
      .CopyObjectAndChildrenAsync(id, this, targetTransport, CancellationToken)
      .ConfigureAwait(false);
    return res;
  }

  public Task<Dictionary<string, bool>> HasObjects(IReadOnlyList<string> objectIds)
  {
    Dictionary<string, bool> ret = new();
    foreach (string objectId in objectIds)
    {
      var filePath = Path.Combine(RootPath, objectId);
      ret[objectId] = File.Exists(filePath);
    }
    return Task.FromResult(ret);
  }

  public override string ToString()
  {
    return $"Disk Transport @{RootPath}";
  }
}
