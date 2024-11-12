using Speckle.Sdk.Dependencies.Serialization;
using Speckle.Sdk.Serialisation.V2;

namespace Speckle.Sdk.Serialization.Tests;

public class DummySqLiteReceiveManager(Dictionary<string, string> savedObjects) : ISQLiteReceiveCacheManager
{
  public string? GetObject(string id) => savedObjects.GetValueOrDefault(id);

  public void SaveObject(BaseItem item) => throw new NotImplementedException();

  public void SaveObjects(List<BaseItem> item) => throw new NotImplementedException();

  public bool HasObject(string objectId) => throw new NotImplementedException();
}
