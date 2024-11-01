using Microsoft.Data.Sqlite;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Dependencies.Serialization;

namespace Speckle.Sdk.Serialisation.V2;

[GenerateAutoInterface]
public class SQLiteSendCacheManager(string streamId) : SQLiteCacheManager(streamId), ISQLiteSendCacheManager
{
  public string? GetObject(string id)
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

  public bool HasObject(string objectId)
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

  public void SaveObjects(List<BaseItem> items)
  {
    using var c = new SqliteConnection(ConnectionString);
    c.Open();
    using var t = c.BeginTransaction();
    const string COMMAND_TEXT = "INSERT OR IGNORE INTO objects(hash, content) VALUES(@hash, @content)";

    using var command = new SqliteCommand(COMMAND_TEXT, c);
    command.Transaction = t;
    var idParam = command.Parameters.Add("@hash", SqliteType.Text);
    var jsonParam = command.Parameters.Add("@content", SqliteType.Text);
    foreach (var item in items)
    {
      idParam.Value = item.Id;
      jsonParam.Value = item.Json;
      command.ExecuteNonQuery();
    }
    t.Commit();
  }
}
