﻿using Speckle.Sdk.SQLite;

namespace Speckle.Sdk.Serialization.Tests;

public class DummySqLiteSendManager : ISqLiteJsonCacheManager
{
  public string? GetObject(string id) => throw new NotImplementedException();

  public void SaveObject(string id, string json) => throw new NotImplementedException();

  public void UpdateObject(string id, string json) => throw new NotImplementedException();

  public void SaveObjects(IEnumerable<(string id, string json)> items) => throw new NotImplementedException();

  public bool HasObject(string objectId) => throw new NotImplementedException();

  public IReadOnlyCollection<string> GetAllObjects() => throw new NotImplementedException();

  public void DeleteObject(string id) => throw new NotImplementedException();
  
  public void Dispose() { }
}
