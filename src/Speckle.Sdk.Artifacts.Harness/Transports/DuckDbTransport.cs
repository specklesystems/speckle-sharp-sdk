using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Artifacts.Harness.Transports;

internal sealed class DuckDbTransport(PackFileManager packFileManager) : ITransport, IDisposable
{
  public string TransportName { get; set; } = nameof(DuckDbTransport);
  public Dictionary<string, object> TransportContext { get; } = new();
  public TimeSpan Elapsed { get; } = TimeSpan.Zero;
  public CancellationToken CancellationToken { get; set; }
  public IProgress<ProgressArgs>? OnProgressAction { get; set; }

  public void Dispose() => packFileManager.Dispose();

  /// <summary>The id of the packfile's authored root object (from its <c>root</c> table).</summary>
  public string GetRootObjectId() => packFileManager.GetRootObjectId();

  public Task<string?> GetObject(string id)
  {
    return Task.FromResult(packFileManager.GetObjectData(id));
  }

  public Task<Dictionary<string, bool>> HasObjects(IReadOnlyList<string> objectIds)
  {
    return Task.FromResult(packFileManager.HasObjects(objectIds));
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
