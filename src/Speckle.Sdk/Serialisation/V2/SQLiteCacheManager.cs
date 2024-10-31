using Microsoft.Data.Sqlite;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation.V2;

[GenerateAutoInterface]
public class SQLiteCacheManager : ISQLiteCacheManager
{
  private readonly string _rootPath;
  private readonly string _connectionString;
  private const string APPLICATION_NAME = "Speckle";
  private const string DATA_FOLDER = "Projects";

  public SQLiteCacheManager(string streamId)
  {
    var basePath = SpecklePathProvider.UserApplicationDataPath();

    try
    {
      var dir = Path.Combine(basePath, APPLICATION_NAME, DATA_FOLDER);
      _rootPath = Path.Combine(dir, $"{streamId}.db");

      Directory.CreateDirectory(dir); //ensure dir is there
    }
    catch (Exception ex)
      when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
    {
      throw new TransportException($"Path was invalid or could not be created {_rootPath}", ex);
    }

    _connectionString = $"Data Source={_rootPath};";
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

    using var c = new SqliteConnection(_connectionString);
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

  public string? GetObject(string id)
  {
    using var c = new SqliteConnection(_connectionString);
    c.Open();
    using var command = new SqliteCommand("SELECT * FROM objects WHERE hash = @hash LIMIT 1 ", c);
    command.Parameters.AddWithValue("@hash", id);
    using var reader = command.ExecuteReader();
    if (reader.Read())
    {
      return reader.GetString(1);
    }
    return null; // pass on the duty of null checks to consumers
  }

  public bool HasObject(string objectId)
  {
    using var c = new SqliteConnection(_connectionString);
    c.Open();
    const string COMMAND_TEXT = "SELECT 1 FROM objects WHERE hash = @hash LIMIT 1 ";
    using var command = new SqliteCommand(COMMAND_TEXT, c);
    command.Parameters.AddWithValue("@hash", objectId);

    using var reader = command.ExecuteReader();
    bool rowFound = reader.Read();
    return rowFound;
  }

  public List<(string, string)> HasObjects(List<(string, string)> objectIds)
  {
    List<(string, string)> result = new();
    using var c = new SqliteConnection(_connectionString);
    c.Open();
    using var tx = c.BeginTransaction();
    const string COMMAND_TEXT = "SELECT 1 FROM objects WHERE hash = @hash LIMIT 1 ";

    foreach (var x in objectIds)
    {
      using var command = new SqliteCommand(COMMAND_TEXT, c);
      command.Transaction = tx;
      command.Parameters.AddWithValue("@hash", x.Item1);
      using var reader = command.ExecuteReader();
      if (reader.Read())
      {
        result.Add(x);
      }
    }

    return result;
  }

  public void SaveObject(string hash, string serializedObject)
  {
    using var c = new SqliteConnection(_connectionString);
    c.Open();
    const string COMMAND_TEXT = "INSERT OR IGNORE INTO objects(hash, content) VALUES(@hash, @content)";

    using var command = new SqliteCommand(COMMAND_TEXT, c);
    command.Parameters.AddWithValue("@hash", hash);
    command.Parameters.AddWithValue("@content", serializedObject);
    command.ExecuteNonQuery();
  }

  public void SaveObjects(List<(string, string)> items)
  {
    using var c = new SqliteConnection(_connectionString);
    c.Open();
    using var tx = c.BeginTransaction();
    const string COMMAND_TEXT = "INSERT OR IGNORE INTO objects(hash, content) VALUES(@hash, @content)";

    foreach (var (hash, content) in items)
    {
      using var command = new SqliteCommand(COMMAND_TEXT, c);
      command.Transaction = tx;
      command.Parameters.AddWithValue("@hash", hash);
      command.Parameters.AddWithValue("@content", content);
      command.ExecuteNonQuery();
    }

    tx.Commit();
  }
}
