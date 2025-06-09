using System.Text;
using Microsoft.Data.Sqlite;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Common;
using Speckle.Sdk.Dependencies;

namespace Speckle.Sdk.SQLite;

public partial interface ISqLiteJsonCacheManager : IDisposable;

[GenerateAutoInterface]
public sealed class SqLiteJsonCacheManager : ISqLiteJsonCacheManager
{
#pragma warning disable CA2211
  public static TimeSpan? SqliteWaitTimeout;
#pragma warning restore CA2211
  private readonly CacheDbCommandPool _pool;

  public static ISqLiteJsonCacheManager FromMemory(int concurrency) => new SqLiteJsonCacheManager(concurrency);

  private SqLiteJsonCacheManager(int concurrency)
  {
    //disable pooling as we pool ourselves
    var builder = new SqliteConnectionStringBuilder
    {
      Pooling = false,
      DataSource = ":memory:",
      Cache = SqliteCacheMode.Shared,
      Mode = SqliteOpenMode.Memory,
    };
    _pool = new CacheDbCommandPool(builder.ToString(), concurrency);
    Initialize();
  }

  public static ISqLiteJsonCacheManager FromFilePath(string path, int concurrency) =>
    new SqLiteJsonCacheManager(path, concurrency);

  private SqLiteJsonCacheManager(string path, int concurrency)
  {
    //disable pooling as we pool ourselves
    var builder = new SqliteConnectionStringBuilder { Pooling = false, DataSource = path };
    _pool = new CacheDbCommandPool(builder.ToString(), concurrency);
    Initialize();
  }

  [AutoInterfaceIgnore]
  public void Dispose() => _pool.Dispose();

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
#pragma warning disable CA2100
      using (
        SqliteCommand cmd6 = new(
          $"PRAGMA busy_timeout={(SqliteWaitTimeout ?? TimeSpan.FromSeconds(5)).Milliseconds};",
          c
        )
      )
#pragma warning restore CA2100
      {
        cmd6.ExecuteNonQuery();
      }
    });
  }

  public IReadOnlyCollection<(string Id, string Json)> GetAllObjects() =>
    _pool.Use(
      CacheOperation.GetAll,
      command =>
      {
        var list = new HashSet<(string, string)>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
          list.Add((reader.GetString(0), reader.GetString(1)));
        }
        return list;
      }
    );

  public void DeleteObject(string id) =>
    _pool.Use(
      CacheOperation.Delete,
      command =>
      {
        command.Parameters.AddWithValue("@hash", id);
        command.ExecuteNonQuery();
      }
    );

  public string? GetObject(string id) =>
    _pool.Use(
      CacheOperation.Get,
      command =>
      {
        command.Parameters.AddWithValue("@hash", id);
        return (string?)command.ExecuteScalar();
      }
    );

  //This does an insert or ignores if already exists
  public void SaveObject(string id, string json)
  {
    id.NotNullOrWhiteSpace();
    json.NotNullOrWhiteSpace();
    _pool.Use(
      CacheOperation.InsertOrIgnore,
      command =>
      {
        command.Parameters.AddWithValue("@hash", id);
        command.Parameters.AddWithValue("@content", json);
        command.ExecuteNonQuery();
      }
    );
  }

  //This does an insert or replaces if already exists
  public void UpdateObject(string id, string json) =>
    _pool.Use(
      CacheOperation.InsertOrReplace,
      command =>
      {
        command.Parameters.AddWithValue("@hash", id);
        command.Parameters.AddWithValue("@content", json);
        command.ExecuteNonQuery();
      }
    );

  public void SaveObjects(IEnumerable<(string id, string json)> items) =>
    _pool.Use(
      CacheOperation.BulkInsertOrIgnore,
      cmd =>
      {
        if (CreateBulkInsert(cmd, items))
        {
          cmd.ExecuteNonQuery();
        }
      }
    );

  private bool CreateBulkInsert(SqliteCommand cmd, IEnumerable<(string id, string json)> items)
  {
    StringBuilder sb = Pools.StringBuilders.Get();
    try
    {
      sb.AppendLine(CacheDbCommands.Commands[(int)CacheOperation.BulkInsertOrIgnore]);
      int i = 0;
      foreach (var (id, json) in items)
      {
        sb.Append($"(@key{i}, @value{i}),");
        cmd.Parameters.AddWithValue($"@key{i}", id);
        cmd.Parameters.AddWithValue($"@value{i}", json);
        i++;
      }

      if (i == 0)
      {
        return false;
      }

      sb.Remove(sb.Length - 1, 1);
      sb.Append(';');
#pragma warning disable CA2100
      cmd.CommandText = sb.ToString();
#pragma warning restore CA2100
    }
    finally
    {
      Pools.StringBuilders.Return(sb);
    }

    return true;
  }

  public bool HasObject(string objectId) =>
    _pool.Use(
      CacheOperation.Has,
      command =>
      {
        command.Parameters.AddWithValue("@hash", objectId);
        using var reader = command.ExecuteReader();
        bool rowFound = reader.Read();
        return rowFound;
      }
    );
}
