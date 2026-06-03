using Speckle.Sdk.Pipelines.Receive;

namespace Speckle.Sdk.Transports;

public sealed class DuckDBTransport(PackFileManager packFileManager) : ITransport, IDisposable
{
  public string TransportName { get; set; } = nameof(DuckDBTransport);
  public Dictionary<string, object> TransportContext { get; } = new();
  public TimeSpan Elapsed { get; } = TimeSpan.Zero;
  public CancellationToken CancellationToken { get; set; }
  public IProgress<ProgressArgs>? OnProgressAction { get; set; }

  public void Dispose() => packFileManager.Dispose();

  public Task<string?> GetObject(string id)
  {
    return Task.FromResult<string?>(packFileManager.GetObjectData(id));
  }

  public Task<Dictionary<string, bool>> HasObjects(IReadOnlyList<string> objectIds)
  {
    return Task.FromResult(objectIds.ToDictionary(x => x, x => true));
  }

  #region Writes (not implemented)
  public Task<string> CopyObjectAndChildren(string id, ITransport targetTransport) =>
    throw new NotImplementedException();

  public Task WriteComplete() => throw new NotImplementedException();

  public void BeginWrite() => throw new NotImplementedException();

  public void EndWrite() => throw new NotImplementedException();

  public void SaveObject(string id, string serializedObject) => throw new NotImplementedException();
  #endregion
}
