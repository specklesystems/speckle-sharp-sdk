using Speckle.Sdk.SQLite;

namespace Speckle.Sdk.Serialization.Tests;

public class DummyCancellationSqLiteSendManager : ISqLiteJsonCacheManager
{
  public string? GetObject(string id) => null;

  public void SaveObject(string id, string json) => throw new NotImplementedException();

  public void UpdateObject(string id, string json) => throw new NotImplementedException();

  public virtual void SaveObjects(IEnumerable<(string id, string json)> items) => throw new NotImplementedException();

  public bool HasObject(string objectId) => throw new NotImplementedException();

  public IReadOnlyCollection<(string, string)> GetAllObjects() => throw new NotImplementedException();

  public void DeleteObject(string id) => throw new NotImplementedException();

  public void Dispose() { }
}
