using Microsoft.Data.Sqlite;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Dependencies.Serialization;

namespace Speckle.Sdk.Serialisation.V2;

[GenerateAutoInterface]
public class SQLiteSendCacheManager(string streamId) : SQLiteCacheManager(streamId), ISQLiteSendCacheManager
{
  private readonly object _lock = new();

  public string? GetObject(string id)
  {
    lock (_lock)
    {
      using var c = new SqliteConnection(ConnectionString);
      c.Open();
      using var command = new SqliteCommand("SELECT * FROM objects WHERE hash = @hash LIMIT 1 ", c);
      command.Parameters.AddWithValue("@hash", id);
      using var reader = command.ExecuteReader();
      if (reader.Read())
      {
        return reader.GetString(1);
      }

      return null; // pass on the duty of null checks to consumers
    }
  }

  public List<BaseItem> HasObjects(List<BaseItem> objectIds)
  {
    lock (_lock)
    {
      List<BaseItem> result = new();
      using var c = new SqliteConnection(ConnectionString);
      c.Open();
      using var tx = c.BeginTransaction();
      const string COMMAND_TEXT = "SELECT 1 FROM objects WHERE hash = @hash LIMIT 1 ";

      foreach (var x in objectIds)
      {
        using var command = new SqliteCommand(COMMAND_TEXT, c);
        command.Transaction = tx;
        command.Parameters.AddWithValue("@hash", x.Id);
        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
          result.Add(x);
        }
      }

      return result;
    }
  }

  public void SaveObjects(List<BaseItem> items)
  {
    lock (_lock)
    {
      using var c = new SqliteConnection(ConnectionString);
      c.Open();
      using var tx = c.BeginTransaction();
      const string COMMAND_TEXT = "INSERT OR IGNORE INTO objects(hash, content) VALUES(@hash, @content)";

      foreach (var item in items)
      {
        using var command = new SqliteCommand(COMMAND_TEXT, c);
        command.Transaction = tx;
        command.Parameters.AddWithValue("@hash", item.Id);
        command.Parameters.AddWithValue("@content", item.Json);
        command.ExecuteNonQuery();
      }

      tx.Commit();
    }
  }
}
