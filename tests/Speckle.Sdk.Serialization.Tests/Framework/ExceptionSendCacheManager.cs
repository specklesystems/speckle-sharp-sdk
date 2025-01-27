using Speckle.Sdk.SQLite;

namespace Speckle.Sdk.Serialization.Tests;

public class ExceptionSendCacheManager(bool? hasObject = null) : ISqLiteJsonCacheManager
{
  public void Dispose() { }

  public IReadOnlyCollection<(string Id, string Json)> GetAllObjects() => throw new NotImplementedException();

  public void DeleteObject(string id) => throw new NotImplementedException();

  public string? GetObject(string id) => null;

  public void SaveObject(string id, string json) => throw new NotImplementedException();

  public void UpdateObject(string id, string json) => throw new NotImplementedException();

  public void SaveObjects(IEnumerable<(string id, string json)> items) => throw new NotImplementedException();

  public bool HasObject(string objectId) => hasObject ?? throw new NotImplementedException();
}
