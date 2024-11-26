using Microsoft.Data.Sqlite;
using Speckle.InterfaceGenerator;

namespace Speckle.Sdk.SQLite;

[GenerateAutoInterface]
public class SqLiteJsonCacheManager : ISqLiteJsonCacheManager
{
  public SqLiteJsonCacheManager(string rootPath)
  {
    ConnectionString = $"Data Source={rootPath};";
    Initialize();
  }

  private void Initialize()
  {
    // NOTE: used for creating partioned object tables.
    //string[] HexChars = new string[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "a", "b", "c", "d", "e", "f" };
    //var cart = new List<string>();
    //foreach (var str in HexChars)
    //  foreach (var str2 in HexChars)
    //    cart.Add(str + str2);

    using var c = new SqliteConnection(ConnectionString);
    c.Open();
    const string COMMAND_TEXT =
      @"
            CREATE TABLE IF NOT EXISTS objects(
              hash TEXT PRIMARY KEY,
              content TEXT
            ) WITHOUT ROWID;
          ";
    using (var command = new SqliteCommand(COMMAND_TEXT, c))
    {
      command.ExecuteNonQuery();
    }

    // Insert Optimisations

    using SqliteCommand cmd0 = new("PRAGMA journal_mode='wal';", c);
    cmd0.ExecuteNonQuery();

    //Note / Hack: This setting has the potential to corrupt the db.
    //cmd = new SqliteCommand("PRAGMA synchronous=OFF;", Connection);
    //cmd.ExecuteNonQuery();

    using SqliteCommand cmd1 = new("PRAGMA count_changes=OFF;", c);
    cmd1.ExecuteNonQuery();

    using SqliteCommand cmd2 = new("PRAGMA temp_store=MEMORY;", c);
    cmd2.ExecuteNonQuery();

    using SqliteCommand cmd3 = new("PRAGMA mmap_size = 30000000000;", c);
    cmd3.ExecuteNonQuery();

    using SqliteCommand cmd4 = new("PRAGMA page_size = 32768;", c);
    cmd4.ExecuteNonQuery();
  }

  protected string ConnectionString { get; }
  
  
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

  public void SaveObject(string id, string json)
  {
    using var c = new SqliteConnection(ConnectionString);
    c.Open();
    const string COMMAND_TEXT = "INSERT OR IGNORE INTO objects(hash, content) VALUES(@hash, @content)";

    using var command = new SqliteCommand(COMMAND_TEXT, c);
    command.Parameters.AddWithValue("@hash",id);
    command.Parameters.AddWithValue("@content", json);
    command.ExecuteNonQuery();
  }

  public void SaveObjects(IEnumerable<(string id, string json)> items)
  {
    using var c = new SqliteConnection(ConnectionString);
    c.Open();
    using var t = c.BeginTransaction();
    const string COMMAND_TEXT = "INSERT OR IGNORE INTO objects(hash, content) VALUES(@hash, @content)";

    using var command = new SqliteCommand(COMMAND_TEXT, c);
    command.Transaction = t;
    var idParam = command.Parameters.Add("@hash", SqliteType.Text);
    var jsonParam = command.Parameters.Add("@content", SqliteType.Text);
    foreach (var (id, json) in items)
    {
      idParam.Value = id;
      jsonParam.Value = json;
      command.ExecuteNonQuery();
    }
    t.Commit();
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
}
