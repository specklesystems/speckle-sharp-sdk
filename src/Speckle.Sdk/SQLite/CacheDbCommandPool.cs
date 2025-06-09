using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;
using Speckle.Sdk.Common;

namespace Speckle.Sdk.SQLite;

//inspired by https://github.com/neosmart/SqliteCache/blob/master/SqliteCache/DbCommandPool.cs
public sealed class CacheDbCommandPool : IDisposable
{
  //this isn't great but it's a stop gap to test sqlite with delays without refactoring to async/await
#pragma warning disable CA2211
  public static TimeSpan? UseDelayTimeSpan;
  public static Action<Exception>? ExceptionOccurred;
#pragma warning restore CA2211

  private readonly ConcurrentBag<SqliteCommand>[] _commands = new ConcurrentBag<SqliteCommand>[CacheDbCommands.Count];
  private readonly ConcurrentBag<SqliteConnection> _connections = new();
  private readonly string _connectionString;

  public CacheDbCommandPool(string connectionString, int concurrency)
  {
    _connectionString = connectionString;
    for (int i = 0; i < _commands.Length; ++i)
    {
      _commands[i] = new ConcurrentBag<SqliteCommand>();
    }
    for (int i = 0; i < concurrency; ++i)
    {
      var connection = new SqliteConnection(_connectionString);
      connection.Open();
      _connections.Add(connection);
    }
  }

  public void Use(Action<SqliteConnection> handler) =>
    Use(conn =>
    {
      handler(conn);
      return true;
    });

  public void Use(CacheOperation type, Action<SqliteCommand> handler) =>
    Use(
      type,
      cmd =>
      {
        handler(cmd);
        return true;
      }
    );

  private T Use<T>(Func<SqliteConnection, T> handler)
  {
    if (!_connections.TryTake(out var db))
    {
      db = new SqliteConnection(_connectionString);
      db.Open();
    }

    try
    {
      return handler(db);
    }
    catch (SqliteException se)
    {
      throw SqLiteJsonCacheException.Create(se);
    }
    finally
    {
      _connections.Add(db);
    }
  }

  public T Use<T>(CacheOperation type, Func<SqliteCommand, T> handler)
  {
    if (UseDelayTimeSpan == null)
    {
      return UseNoDelay(type, handler);
    }

    return UseDelay(type, handler);
  }

  private T UseNoDelay<T>(CacheOperation type, Func<SqliteCommand, T> handler) =>
    Use(conn =>
    {
      var pool = _commands[(int)type];
      if (!pool.TryTake(out var command))
      {
#pragma warning disable CA2100
        command = new SqliteCommand(CacheDbCommands.Commands[(int)type], conn);
#pragma warning restore CA2100
      }

      try
      {
        command.Connection = conn;
        return handler(command);
      }
      catch (SqliteException se)
      {
        ExceptionOccurred?.Invoke(se);
        throw SqLiteJsonCacheException.Create(se);
      }
      finally
      {
        command.Connection = null;
        command.Parameters.Clear();
        pool.Add(command);
      }
    });

  private T UseDelay<T>(CacheOperation type, Func<SqliteCommand, T> handler) =>
    Use(conn =>
    {
      var pool = _commands[(int)type];
      if (!pool.TryTake(out var command))
      {
#pragma warning disable CA2100
        command = new SqliteCommand(CacheDbCommands.Commands[(int)type], conn);
#pragma warning restore CA2100
      }

      using var transaction = conn.BeginTransaction();
      try
      {
        command.Connection = conn;
        command.Transaction = transaction;
        Thread.Sleep(UseDelayTimeSpan.NotNull());
        var ret = handler(command);
        transaction.Commit();
        return ret;
      }
      catch (SqliteException se)
      {
        ExceptionOccurred?.Invoke(se);
        throw SqLiteJsonCacheException.Create(se);
      }
      finally
      {
        command.Connection = null;
        command.Parameters.Clear();
        pool.Add(command);
      }
    });

  public void Dispose()
  {
    foreach (var pool in _commands)
    {
      while (pool.TryTake(out var cmd))
      {
        cmd.Dispose();
      }
    }
    while (_connections.TryTake(out var conn))
    {
      conn.Close();
      conn.Dispose();
    }
  }
}
