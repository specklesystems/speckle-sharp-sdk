using Speckle.Sdk.Dependencies.Serialization;
using Speckle.Sdk.Serialisation.V2;

namespace Speckle.Sdk.Serialization.Tests;

public class DummySqLiteSendManager : ISQLiteSendCacheManager
{
  public string? GetObject(string id) => throw new NotImplementedException();

  public bool HasObject(string objectId) => throw new NotImplementedException();

  public void SaveObjects(List<BaseItem> items) => throw new NotImplementedException();
}
