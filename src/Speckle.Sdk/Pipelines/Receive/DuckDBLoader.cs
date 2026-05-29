using System.Data.Common;
using System.Text.Json;
using DuckDB.NET.Data;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Pipelines.Receive;

internal sealed class PackFileManager : IDisposable
#if NET5_0_OR_GREATER
    , IAsyncDisposable
#endif
{
  private readonly DuckDBConnection _connection;
  private readonly JsonSerializerOptions _options;

  public PackFileManager(FileInfo file)
  {
    _connection = new DuckDBConnection($"DataSource={file.FullName};ACCESS_MODE=READ_ONLY");
    _options = new JsonSerializerOptions();
    _options.Converters.Add(new SpeckleJsonConverter(new()));
  }

  public async Task OpenAsync()
  {
    await _connection.OpenAsync().ConfigureAwait(false);
  }

  public void Open()
  {
    _connection.Open();
  }

  public string GetObject()
  {
    using DuckDBCommand command = _connection.CreateCommand();
    command.CommandText = "SELECT data FROM objects";

    using DbDataReader reader = command.ExecuteReader();

    while (reader.Read())
    {
      string utf8Json = reader.GetString(0); //TODO: benchmark performance allocating strings or byte arrays here
    }
  }

  public IEnumerable<string> GetObjects()
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

  public void Dispose() => _connection.Dispose();

#if NET5_0_OR_GREATER
  public async ValueTask DisposeAsync() => await _connection.DisposeAsync().ConfigureAwait(false);
#endif
}
