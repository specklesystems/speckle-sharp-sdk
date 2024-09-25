using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Performance.Testing;

public class NullTransport : ITransport
{
  public string TransportName { get; set; } = "";
  public Dictionary<string, object> TransportContext { get; } = new();
  public TimeSpan Elapsed { get; } = TimeSpan.Zero;
  public CancellationToken CancellationToken { get; set; }
  public Action<ProgressArgs>? OnProgressAction { get; set; }

  public void BeginWrite() { }

  public void EndWrite() { }

  public void SaveObject(string id, string serializedObject) { }

  public Task WriteComplete()
  {
    return Task.CompletedTask;
  }

  public Task<string?> GetObject(string id) => throw new NotImplementedException();

  public Task<string> CopyObjectAndChildren(
    string id,
    ITransport targetTransport,
    Action<int>? onTotalChildrenCountKnown = null
  ) => throw new NotImplementedException();

  public Task<Dictionary<string, bool>> HasObjects(IReadOnlyList<string> objectIds) =>
    throw new NotImplementedException();
}
