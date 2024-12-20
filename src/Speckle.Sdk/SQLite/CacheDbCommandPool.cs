﻿using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;

namespace Speckle.Sdk.SQLite;

//inspired by https://github.com/neosmart/SqliteCache/blob/master/SqliteCache/DbCommandPool.cs
public sealed class CacheDbCommandPool : IDisposable
{
  private const int INITIAL_CONCURRENCY = 4;
  private readonly ConcurrentBag<SqliteCommand>[] _commands = new ConcurrentBag<SqliteCommand>[CacheDbCommands.Count];
  private readonly ConcurrentBag<SqliteConnection> _connections = new();
  private readonly string _connectionString;

  public CacheDbCommandPool(string connectionString)
  {
    _connectionString = connectionString;
    for (int i = 0; i < _commands.Length; ++i)
    {
      _commands[i] = new ConcurrentBag<SqliteCommand>();
    }
    for (int i = 0; i < INITIAL_CONCURRENCY; ++i)
    {
      var connection = new SqliteConnection(_connectionString);
      connection.Open();
      _connections.Add(connection);
    }
  }

  public void Use(CacheOperation type, Action<SqliteCommand> handler)
  {
    Use<bool>(
      type,
      (cmd) =>
      {
        handler(cmd);
        return true;
      }
    );
  }

  public T Use<T>(Func<SqliteConnection, T> handler)
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
    finally
    {
      _connections.Add(db);
    }
  }

  public T Use<T>(CacheOperation type, Func<SqliteCommand, T> handler)
  {
    return Use(
      (conn) =>
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
        finally
        {
          command.Connection = null;
          command.Parameters.Clear();
          pool.Add(command);
        }
      }
    );
  }

  public void Dispose()
  {
    foreach (var pool in _commands)
    {
      while (pool.TryTake(out var cmd))
      {
        cmd.Dispose();
      }
    }

    foreach (var conn in _connections)
    {
      conn.Close();
      conn.Dispose();
    }
  }
}
