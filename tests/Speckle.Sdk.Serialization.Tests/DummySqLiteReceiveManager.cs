using Speckle.Sdk.SQLite;

namespace Speckle.Sdk.Serialization.Tests;

public class DummySqLiteReceiveManager(Dictionary<string, string> savedObjects) : ISqLiteJsonCacheManager
{
  public string? GetObject(string id) => savedObjects.GetValueOrDefault(id);

  public void SaveObject(string id, string json) => throw new NotImplementedException();

  public void SaveObjects(IEnumerable<(string id, string json)> items) => throw new NotImplementedException();

  public bool HasObject(string objectId) => throw new NotImplementedException();
}
