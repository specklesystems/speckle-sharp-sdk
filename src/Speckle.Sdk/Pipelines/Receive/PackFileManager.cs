using System.Collections.Concurrent;
using System.Data.Common;
using System.Runtime.CompilerServices;
using DuckDB.NET.Data;
using Speckle.Sdk.Logging;

namespace Speckle.Sdk.Pipelines.Receive;

internal readonly record struct ThreadContext(DuckDBConnection Connection, DuckDBCommand GetObjectSingleCommand);

public sealed class PackFileManager(FileInfo file, ISdkActivityFactory activityFactory) : IDisposable
#if NET5_0_OR_GREATER
    , IAsyncDisposable
#endif
{
  private readonly ConcurrentDictionary<int, ThreadContext> _threadContexts = new();
  private ThreadContext CurrentContext => GetOrCreateContext();

  public string GetObjectData(string id)
  {
    using ISdkActivity? activity = activityFactory.Start();
    try
    {
      DuckDBCommand command = CurrentContext.GetObjectSingleCommand;
      command.Parameters.Clear();
      command.Parameters.Add(new(id));

      using DbDataReader reader = command.ExecuteReader();

      if (!reader.Read())
      {
        throw new KeyNotFoundException($"Failed to find object with id {id}");
      }

      string json = reader.GetString(0);
      activity?.SetStatus(SdkActivityStatusCode.Ok);
      return json;
    }
    catch (Exception ex)
    {
      activity?.SetStatus(SdkActivityStatusCode.Error);
      activity?.RecordException(ex);
      throw;
    }
  }

  public IEnumerable<(string id, string speckle_type, string json)> GetObjects(CancellationToken cancellationToken)
  {
    using DuckDBCommand command = CurrentContext.Connection.CreateCommand();
    //language=PostgreSQL
    command.CommandText =
      "SELECT id, data, speckle_type FROM objects WHERE speckle_type != 'Speckle.Core.Models.DataChunk' ORDER BY rowid DESC";
    command.UseStreamingMode = true;

    using DbDataReader reader = command.ExecuteReader();
    while (reader.Read())
    {
      cancellationToken.ThrowIfCancellationRequested();

      string id = reader.GetString(0);
      string json = reader.GetString(1);
      string speckleType = reader.GetString(2);
      yield return (id, speckleType, json);
    }
  }

  public long GetEstimatedObjectCount()
  {
    using DuckDBCommand command = CurrentContext.Connection.CreateCommand();
    //language=PostgreSQL
    command.CommandText = "SELECT estimated_size FROM duckdb_tables() WHERE table_name = 'objects';";

    using DbDataReader reader = command.ExecuteReader();

    if (!reader.Read())
    {
      throw new KeyNotFoundException("Failed to get estimated size of objects table");
    }

    return reader.GetInt64(0);
  }

  public async IAsyncEnumerable<(string id, string speckle_type, string json)> GetObjectsAsync(
    [EnumeratorCancellation] CancellationToken cancellationToken
  )
  {
    //language=PostgreSQL
    const string QUERY =
      "SET preserve_insertion_order = false;SELECT id, data, speckle_type FROM objects WHERE speckle_type != 'Speckle.Core.Models.DataChunk' ORDER BY rowid DESC";
    using DuckDBCommand command = CurrentContext.Connection.CreateCommand();
    command.CommandText = QUERY;
    command.UseStreamingMode = true;

    using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
    {
      string id = reader.GetString(0);
      string json = reader.GetString(1);
      string speckleType = reader.GetString(2);
      yield return (id, speckleType, json);
    }
  }

  public string GetRootObjectId()
  {
    using var activity = activityFactory.Start();
    try
    {
      //language=PostgreSQL
      const string QUERY = """
        SELECT id FROM root LIMIT 1
        """;

      using DuckDBCommand command = CurrentContext.Connection.CreateCommand();
      command.CommandText = QUERY;

      using DbDataReader reader = command.ExecuteReader();

      if (!reader.Read())
      {
        throw new KeyNotFoundException();
      }

      string id = reader.GetString(0);
      activity?.SetStatus(SdkActivityStatusCode.Ok);
      return id;
    }
    catch (Exception ex)
    {
      activity?.SetStatus(SdkActivityStatusCode.Error);
      activity?.RecordException(ex);
      throw;
    }
  }

  private ThreadContext GetOrCreateContext()
  {
    var id = Environment.CurrentManagedThreadId;
    if (_threadContexts.TryGetValue(id, out ThreadContext context))
    {
      return context;
    }
    var connection = new DuckDBConnection($"DataSource={file.FullName};ACCESS_MODE=READ_ONLY");
    //language=PostgreSQL
    const string QUERY = "SELECT data FROM objects where id = ? LIMIT 1";
    var getObjectSingleCommand = connection.CreateCommand();
    getObjectSingleCommand.CommandText = QUERY;

    connection.Open();
    return _threadContexts[id] = new ThreadContext(connection, getObjectSingleCommand);
  }

  public void Dispose()
  {
    foreach (var context in _threadContexts.Values)
    {
      context.GetObjectSingleCommand.Dispose();
      context.Connection.Dispose();
    }
    _threadContexts.Clear();
  }

#if NET5_0_OR_GREATER
  public async ValueTask DisposeAsync()
  {
    foreach (var context in _threadContexts.Values)
    {
      await context.GetObjectSingleCommand.DisposeAsync().ConfigureAwait(false);
      await context.Connection.DisposeAsync().ConfigureAwait(false);
    }
    _threadContexts.Clear();
  }
#endif
}
