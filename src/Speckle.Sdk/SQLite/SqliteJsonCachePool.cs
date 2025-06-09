using Microsoft.Data.Sqlite;
using Speckle.InterfaceGenerator;

namespace Speckle.Sdk.SQLite;

public partial interface ISqliteJsonCachePool : IDisposable
{
  T Use<T>(CacheOperation type, Func<SqliteCommand, T> handler);
}

[GenerateAutoInterface]
public sealed class SqliteJsonCachePool : ISqliteJsonCachePool
{
  private readonly CacheDbCommandPool _pool;

  public SqliteJsonCachePool(string path, int concurrency)
  {
    Path = path;
    Concurrency = concurrency;
    //disable pooling as we pool ourselves
    var builder = new SqliteConnectionStringBuilder { Pooling = false, DataSource = path };
    _pool = new CacheDbCommandPool(builder.ToString(), concurrency);
    Initialize();
  }

  public string Path { get; }
  public int Concurrency { get; }

  private void Initialize()
  {
    // NOTE: used for creating partioned object tables.
    //string[] HexChars = new string[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "a", "b", "c", "d", "e", "f" };
    //var cart = new List<string>();
    //foreach (var str in HexChars)
    //  foreach (var str2 in HexChars)
    //    cart.Add(str + str2);

    _pool.Use(c =>
    {
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

      //Note / Hack: This setting has the potential to corrupt the db.
      //cmd = new SqliteCommand("PRAGMA synchronous=OFF;", Connection);
      //cmd.ExecuteNonQuery();

      using (SqliteCommand cmd1 = new("PRAGMA count_changes=OFF;", c))
      {
        cmd1.ExecuteNonQuery();
      }

      using (SqliteCommand cmd2 = new("PRAGMA temp_store=MEMORY;", c))
      {
        cmd2.ExecuteNonQuery();
      }

      using (SqliteCommand cmd3 = new("PRAGMA mmap_size = 30000000000;", c))
      {
        cmd3.ExecuteNonQuery();
      }

      using (SqliteCommand cmd4 = new("PRAGMA page_size = 32768;", c))
      {
        cmd4.ExecuteNonQuery();
      }

      using (SqliteCommand cmd5 = new("PRAGMA journal_mode='wal';", c))
      {
        cmd5.ExecuteNonQuery();
      }
      //do this to wait 5 seconds to avoid db lock exceptions, this is 0 by default
      using (SqliteCommand cmd6 = new("PRAGMA busy_timeout=5000;", c))
      {
        cmd6.ExecuteNonQuery();
      }
    });
  }

  public void Use(Action<SqliteConnection> handler) => _pool.Use(handler);

  public void Use(CacheOperation type, Action<SqliteCommand> handler) => _pool.Use(type, handler);

  [AutoInterfaceIgnore]
  public T Use<T>(CacheOperation type, Func<SqliteCommand, T> handler) => _pool.Use(type, handler);

  [AutoInterfaceIgnore]
  public void Dispose() => _pool.Dispose();
}
