using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialization.Tests;

public class TestTransport2(IDictionary<string, string> objects) : ITransport
{
  public IDictionary<string, string> Objects { get; } = objects;

  public string TransportName
  {
    get => "Test";
    set { }
  }

  public Dictionary<string, object> TransportContext { get; }
  public TimeSpan Elapsed { get; }
  public int SavedObjectCount { get; }
  public CancellationToken CancellationToken { get; set; }
  public IProgress<ProgressArgs>? OnProgressAction { get; set; }
  public Action<string, Exception>? OnErrorAction { get; set; }

  public void BeginWrite() => throw new NotImplementedException();

  public void EndWrite() => throw new NotImplementedException();

  public void SaveObject(string id, string serializedObject) => Objects[id] = serializedObject;

  public Task WriteComplete() => throw new NotImplementedException();

  public Task<string?> GetObject(string id) => Task.FromResult(Objects.TryGetValue(id, out string? o) ? o : null);

  public Task<string> CopyObjectAndChildren(string id, ITransport targetTransport) =>
    throw new NotImplementedException();

  public Task<Dictionary<string, bool>> HasObjects(IReadOnlyList<string> objectIds) =>
    throw new NotImplementedException();
}
