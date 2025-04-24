using Microsoft.Data.Sqlite;
using Speckle.Sdk.SQLite;

namespace Speckle.Sdk.Tests.Unit.SQLite;

public class SqLiteJsonCacheExceptionTests
{
  [Fact]
  public void ExpectedExceptionFires_Void()
  {
    using var pool = new CacheDbCommandPool("DataSource=:memory:", 1);
    Assert.Throws<SqLiteJsonCacheException>(() =>
      pool.Use(CacheOperation.Get, new Action<SqliteCommand>(_ => throw new SqliteException("test", 1, 1)))
    );
  }

  [Fact]
  public void ExpectedExceptionFires_Return()
  {
    using var pool = new CacheDbCommandPool("DataSource=:memory:", 1);
    Assert.Throws<SqLiteJsonCacheException>(() =>
      pool.Use(CacheOperation.Get, new Func<SqliteCommand, bool>(_ => throw new SqliteException("test", 1, 1)))
    );
  }
}
