using Speckle.Core.Transports;

namespace Speckle.Core.Serialization.Tests;

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
    set
    {
    }
  }

  public Dictionary<string, object> TransportContext { get; }
  public TimeSpan Elapsed { get; }
  public int SavedObjectCount { get; }
  public CancellationToken CancellationToken { get; set; }
  public Action<string, int>? OnProgressAction { get; set; }
  public Action<string, Exception>? OnErrorAction { get; set; }
  public void BeginWrite() => throw new NotImplementedException();

  public void EndWrite() => throw new NotImplementedException();

  public void SaveObject(string id, string serializedObject) => throw new NotImplementedException();

  public void SaveObject(string id, ITransport sourceTransport) => throw new NotImplementedException();

  public Task WriteComplete() => throw new NotImplementedException();

  public string? GetObject(string id) => Objects.TryGetValue(id, out string? o) ? o : null;

  public Task<string> CopyObjectAndChildren(string id, ITransport targetTransport, Action<int>? onTotalChildrenCountKnown = null) => throw new NotImplementedException();

  public Task<Dictionary<string, bool>> HasObjects(IReadOnlyList<string> objectIds) => throw new NotImplementedException();
}
