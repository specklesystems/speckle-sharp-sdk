using Microsoft.Data.Sqlite;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Dependencies.Serialization;

namespace Speckle.Sdk.Serialisation.V2;

[GenerateAutoInterface]
public class SQLiteReceiveCacheManager(string streamId) : SQLiteCacheManager(streamId), ISQLiteReceiveCacheManager
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

  public void SaveObject(BaseItem item)
  {
    lock (_lock)
    {
      using var c = new SqliteConnection(ConnectionString);
      c.Open();
      const string COMMAND_TEXT = "INSERT OR IGNORE INTO objects(hash, content) VALUES(@hash, @content)";

      using var command = new SqliteCommand(COMMAND_TEXT, c);
      command.Parameters.AddWithValue("@hash", item.Id);
      command.Parameters.AddWithValue("@content", item.Json);
      command.ExecuteNonQuery();
    }
  }

  public bool HasObject(string objectId)
  {
    lock (_lock)
    {
      using var c = new SqliteConnection(ConnectionString);
      c.Open();
      const string COMMAND_TEXT = "SELECT 1 FROM objects WHERE hash = @hash LIMIT 1 ";
      using var command = new SqliteCommand(COMMAND_TEXT, c);
      command.Parameters.AddWithValue("@hash", objectId);

      using var reader = command.ExecuteReader();
      bool rowFound = reader.Read();
      return rowFound;
    }
  }
}
