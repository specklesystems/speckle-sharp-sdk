﻿using Speckle.Sdk.SQLite;

namespace Speckle.Sdk.Testing.Framework;

public sealed class DummySqLiteReceiveManager(IReadOnlyDictionary<string, string> savedObjects)
  : ISqLiteJsonCacheManager
{
  public void Dispose() { }

  public IReadOnlyCollection<(string, string)> GetAllObjects() => throw new NotImplementedException();

  public void DeleteObject(string id) => throw new NotImplementedException();

  public string? GetObject(string id) => savedObjects.GetValueOrDefault(id);

  public void SaveObject(string id, string json) => throw new NotImplementedException();

  public void UpdateObject(string id, string json) => throw new NotImplementedException();

  public void SaveObjects(IEnumerable<(string id, string json)> items) => throw new NotImplementedException();

  public bool HasObject(string objectId) => throw new NotImplementedException();
}
