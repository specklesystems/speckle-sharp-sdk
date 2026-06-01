using System.Data.Common;
using System.Text.Json;
using DuckDB.NET.Data;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;
using Speckle.Sdk.Pipelines.Receive.JsonConverters;

namespace Speckle.Sdk.Pipelines.Receive;

public sealed class PackFileManager : IDisposable
#if NET5_0_OR_GREATER
    , IAsyncDisposable
#endif
{
  private readonly DuckDBConnection _connection;
  private readonly JsonSerializerOptions _options;
  private DuckDBCommand _getObjectSingleCommand;

  public PackFileManager(FileInfo file)
  {
    _connection = new DuckDBConnection($"DataSource={file.FullName};ACCESS_MODE=READ_ONLY");
    _options = new JsonSerializerOptions();
    _options.Converters.Add(new SpeckleObjectJsonConverter(this));
    _options.Converters.Add(new ChunkedDoubleListJsonConverter(this));
    _options.Converters.Add(new ChunkedInt32ListJsonConverter(this));
    _options.Converters.Add(new SpeckleMatrix4x4JsonConverter());
    _options.Converters.Add(new ColorArgbConverter());
  }

  public async Task OpenAsync(CancellationToken cancellationToken)
  {
    await _connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    ConfigureCommands();
  }

  public void Open()
  {
    _connection.Open();
    ConfigureCommands();
  }

  private void ConfigureCommands()
  {
    {
      //language=PostgreSQL
      const string QUERY = "SELECT data FROM objects where id = ? LIMIT 1";
      _getObjectSingleCommand = _connection.CreateCommand();
      _getObjectSingleCommand.CommandText = QUERY;
    }
  }

  public Base GetObject(string id)
  {
    string json = GetObjectString(id);
    return JsonSerializer.Deserialize<Base>(json, _options).NotNull();
  }

  public T GetObjectGeneric<T>(string id)
    where T : class
  {
    string json = GetObjectString(id);
    return JsonSerializer.Deserialize<T>(json, _options).NotNull();
  }

  public string GetObjectString(string id)
  {
    _getObjectSingleCommand.Parameters.Clear();
    _getObjectSingleCommand.Parameters.Add(new(id));

    using DbDataReader reader = _getObjectSingleCommand.ExecuteReader();

    if (!reader.Read())
    {
      throw new KeyNotFoundException($"Failed to find object with id {id}");
    }

    return reader.GetString(0); //TODO: benchmark performance allocating strings or byte arrays here
  }

  public Base GetCompleteObjectsTree()
  {
    //language=PostgreSQL
    const string QUERY = """
      SELECT data FROM root LIMIT 1
      """;
    using DuckDBCommand command = _connection.CreateCommand();
    command.CommandText = QUERY;

    using DbDataReader reader = command.ExecuteReader();

    if (!reader.Read())
    {
      throw new KeyNotFoundException();
    }
    string json = reader.GetString(0);
    return JsonSerializer.Deserialize<Base>(json, _options).NotNull();
  }

  public IEnumerable<Base> GetObjectsDepthFirst() //Too expensive
  {
    //language=PostgreSQL
    const string QUERY = """
      SELECT
        c.key        AS referenced_id,
        c.value::INT AS closure_value,
        t.data       AS data
      FROM objects t
      CROSS JOIN LATERAL json_each(t.data -> '__closure') AS c(key, value)
      WHERE t.id = (SELECT id FROM root LIMIT 1)
      ORDER BY closure_value DESC
      """;

    //language=PostgreSQL
    // const string QUERY = """
    //   SELECT
    //     c.key,
    //     c.value
    //   FROM root r,
    //   json_each(r.data -> '__closure') c;
    //   """;
    using DuckDBCommand command = _connection.CreateCommand();
    command.CommandText = QUERY;
    command.UseStreamingMode = true;

    using DbDataReader reader = command.ExecuteReader();

    while (reader.Read())
    {
      string utf8Json = reader.GetString(2); //TODO: benchmark performance allocating strings or byte arrays here
      yield return JsonSerializer.Deserialize<Base>(utf8Json, _options).NotNull();
    }
  }

  public IEnumerable<Base> GetObjects()
  {
    using DuckDBCommand command = _connection.CreateCommand();
    command.CommandText = "SELECT data FROM objects";

    using DbDataReader reader = command.ExecuteReader();

    while (reader.Read())
    {
      string utf8Json = reader.GetString(0); //TODO: benchmark performance allocating strings or byte arrays here
      yield return JsonSerializer.Deserialize<Base>(utf8Json, _options).NotNull();
    }
  }

  public void Dispose()
  {
    _connection.Dispose();
    _getObjectSingleCommand.Dispose();
  }

#if NET5_0_OR_GREATER
  public async ValueTask DisposeAsync()
  {
    await _connection.DisposeAsync().ConfigureAwait(false);
    await _getObjectSingleCommand.DisposeAsync().ConfigureAwait(false);
    ;
  }
#endif
}
