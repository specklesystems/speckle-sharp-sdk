using Microsoft.Data.Sqlite;
using Speckle.Sdk.Serialisation.Utilities;

namespace Speckle.Sdk.Serialisation.V2;

public abstract class SQLiteCacheManager
{
  private readonly string _rootPath;

  protected SQLiteCacheManager(string streamId)
  {
    _rootPath = SqlitePaths.GetDBPath(streamId);

    ConnectionString = $"Data Source={_rootPath};";
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
}
