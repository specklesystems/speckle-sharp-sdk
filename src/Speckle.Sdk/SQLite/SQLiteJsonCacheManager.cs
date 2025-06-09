using System.Text;
using Microsoft.Data.Sqlite;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Common;
using Speckle.Sdk.Dependencies;

namespace Speckle.Sdk.SQLite;
public partial interface ISqLiteJsonCacheManager : IDisposable;
[GenerateAutoInterface]
public sealed class SqLiteJsonCacheManager(ISqliteJsonCachePool pool, bool dispose) : ISqLiteJsonCacheManager
{
  public ISqliteJsonCachePool Pool => pool;
  public IReadOnlyCollection<(string Id, string Json)> GetAllObjects() =>
    pool.Use(
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
    pool.Use(
      CacheOperation.Delete,
      command =>
      {
        command.Parameters.AddWithValue("@hash", id);
        command.ExecuteNonQuery();
      }
    );

  public string? GetObject(string id) =>
    pool.Use(
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
    pool.Use(
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
    pool.Use(
      CacheOperation.InsertOrReplace,
      command =>
      {
        command.Parameters.AddWithValue("@hash", id);
        command.Parameters.AddWithValue("@content", json);
        command.ExecuteNonQuery();
      }
    );

  public void SaveObjects(IEnumerable<(string id, string json)> items) =>
    pool.Use(
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
    pool.Use(
      CacheOperation.Has,
      command =>
      {
        command.Parameters.AddWithValue("@hash", objectId);
        using var reader = command.ExecuteReader();
        bool rowFound = reader.Read();
        return rowFound;
      }
    );

  [AutoInterfaceIgnore]
  public void Dispose()
  {
    if (dispose)
    {
      pool.Dispose();
    }
  }
}
