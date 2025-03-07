namespace Speckle.Sdk.SQLite;

public enum CacheOperation
{
  InsertOrIgnore,
  InsertOrReplace,
  Has,
  Get,
  Delete,
  GetAll,
  BulkInsertOrIgnore,
}

public static class CacheDbCommands
{
  public static readonly string[] Commands;
  public static readonly int Count = Enum.GetValues(typeof(CacheOperation)).Length;

#pragma warning disable CA1810
  static CacheDbCommands()
#pragma warning restore CA1810
  {
    Commands = new string[Count];

    Commands[(int)CacheOperation.InsertOrIgnore] =
      "INSERT OR IGNORE INTO objects(hash, content) VALUES(@hash, @content)";
    Commands[(int)CacheOperation.InsertOrReplace] = "REPLACE INTO objects(hash, content) VALUES(@hash, @content)";
    Commands[(int)CacheOperation.Has] = "SELECT 1 FROM objects WHERE hash = @hash LIMIT 1";
    Commands[(int)CacheOperation.Get] = "SELECT content FROM objects WHERE hash = @hash LIMIT 1";
    Commands[(int)CacheOperation.Delete] = "DELETE FROM objects WHERE hash = @hash";
    Commands[(int)CacheOperation.GetAll] = "SELECT hash, content FROM objects";

    Commands[(int)CacheOperation.BulkInsertOrIgnore] = "INSERT OR IGNORE INTO objects (hash, content) VALUES ";
  }
}
