using Speckle.Sdk.SQLite;

namespace Speckle.Sdk.Serialization.Tests.Framework;

public class ExceptionSendCacheManager(bool? hasObject = null, int? exceptionsAfter = null) : ISqLiteJsonCacheManager
{
  public string Path => "ExceptionSendCacheManager";
  private readonly object _lock = new();
  private int _count;

  public void Dispose() { }

  public IReadOnlyCollection<(string Id, string Json)> GetAllObjects() => throw new NotImplementedException();

  public void DeleteObject(string id) => CheckExceptions();

  public string? GetObject(string id) => null;

  public void SaveObject(string id, string json) => CheckExceptions();

  public void UpdateObject(string id, string json) => CheckExceptions();

  public void SaveObjects(IEnumerable<(string id, string json)> items) => CheckExceptions();

  public bool HasObject(string objectId) => hasObject ?? throw new NotImplementedException();

  private void CheckExceptions()
  {
    lock (_lock)
    {
      if (exceptionsAfter is not null)
      {
        if (exceptionsAfter.Value > _count)
        {
          _count++;
        }
        else
        {
          throw new Exception("Count exceeded");
        }
      }
      else
      {
        throw new NotImplementedException();
      }
    }
  }
}
