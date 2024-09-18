using Microsoft.Data.Sqlite;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation;

public record SqliteManagerOptions(bool Enabled = true, string? Path = null, string? ApplicationName = null, string? Scope = null);
public class SqliteManager
{
  private readonly string _rootPath;
  private readonly string _connectionString;

  private readonly string _basePath;
  private readonly string _applicationName;
  private readonly string _scope;
  private SqliteConnection Connection { get; set; }

  public SqliteManager(SqliteManagerOptions options)
  {
    _basePath = options.Path ?? SpecklePathProvider.UserApplicationDataPath();
    _applicationName = options.ApplicationName ?? "Speckle";
    _scope = options.Scope ?? "Data";
    try
    {
      var dir = Path.Combine(_basePath, _applicationName);
      _rootPath = Path.Combine(dir, $"{_scope}.db");

      Directory.CreateDirectory(dir); //ensure dir is there
    }
    catch (Exception ex)
      when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
    {
      throw new TransportException($"Path was invalid or could not be created {_rootPath}", ex);
    }
    _connectionString = $"Data Source={_rootPath};";
    Connection = Initialize();
    Connection.Open();
  }

  /// <exception cref="SqliteException">Failed to initialize connection to the SQLite DB</exception>
  private SqliteConnection Initialize()
  {
    // NOTE: used for creating partioned object tables.
    //string[] HexChars = new string[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "a", "b", "c", "d", "e", "f" };
    //var cart = new List<string>();
    //foreach (var str in HexChars)
    //  foreach (var str2 in HexChars)
    //    cart.Add(str + str2);

    using (var c = new SqliteConnection(_connectionString))
    {
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
    }

    return new SqliteConnection(_connectionString);
  }

  public IEnumerable<(string, bool)> HasObjects(IReadOnlyList<string> objectIds, CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();
    const string COMMAND_TEXT = "SELECT 1 FROM objects WHERE hash = @hash LIMIT 1 ";
    using var command = new SqliteCommand(COMMAND_TEXT, Connection);

    foreach (string objectId in objectIds)
    {
      cancellationToken.ThrowIfCancellationRequested();

      command.Parameters.Clear();
      command.Parameters.AddWithValue("@hash", objectId);

      using var reader = command.ExecuteReader();
      bool rowFound = reader.Read();
      yield return (objectId, rowFound);
    }
  }

  public IEnumerable<(string, string?)> GetObjects(IEnumerable<string> ids, CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();
    using var command = Connection.CreateCommand();
    command.CommandText = "SELECT * FROM objects WHERE hash = @hash LIMIT 1 ";

    foreach (var id in ids)
    {
      cancellationToken.ThrowIfCancellationRequested();
      command.Parameters.AddWithValue("@hash", id);
      yield return (id, (string?)command.ExecuteScalar());
      command.Parameters.Clear();
    }
  }
}
