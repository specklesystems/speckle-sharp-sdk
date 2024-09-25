using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialization.Tests;

public class TestTransport : ITransport
{
  public IDictionary<string, string> Objects { get; }

  public TestTransport(IDictionary<string, string> objects)
  {
    Objects = objects;
  }

  public string TransportName
  {
    get => "Test";
    set { }
  }

  public Dictionary<string, object> TransportContext { get; }
  public TimeSpan Elapsed { get; }
  public int SavedObjectCount { get; }
  public CancellationToken CancellationToken { get; set; }
  public Action<ProgressArgs>? OnProgressAction { get; set; }
  public Action<string, Exception>? OnErrorAction { get; set; }

  public void BeginWrite() => throw new NotImplementedException();

  public void EndWrite() => throw new NotImplementedException();

  public void SaveObject(string id, string serializedObject) => Objects[id] = serializedObject;

  public ValueTask WriteComplete() => throw new NotImplementedException();

  public ValueTask<string?> GetObject(string id) =>
    ValueTask.FromResult(Objects.TryGetValue(id, out string? o) ? o : null);

  public ValueTask<string> CopyObjectAndChildren(
    string id,
    ITransport targetTransport,
    Action<int>? onTotalChildrenCountKnown = null
  ) => throw new NotImplementedException();

  public ValueTask<Dictionary<string, bool>> HasObjects(IReadOnlyList<string> objectIds) =>
    throw new NotImplementedException();
}
