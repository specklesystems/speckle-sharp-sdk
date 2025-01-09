using Microsoft.Data.Sqlite;
using NUnit.Framework;
using Speckle.Sdk.SQLite;

namespace Speckle.Sdk.Tests.Unit.SQLite;

[TestFixture]
public class SqLiteJsonCacheExceptionTests
{
  [Test]
  public void ExpectedExceptionFires_Void()
  {
    using var pool = new CacheDbCommandPool("DataSource=:memory:", 1);
    Assert.Throws<SqLiteJsonCacheException>(
      () => pool.Use(CacheOperation.Get, new Action<SqliteCommand>(_ => throw new SqliteException("test", 1, 1)))
    );
  }

  [Test]
  public void ExpectedExceptionFires_Return()
  {
    using var pool = new CacheDbCommandPool("DataSource=:memory:", 1);
    Assert.Throws<SqLiteJsonCacheException>(
      () => pool.Use(CacheOperation.Get, new Func<SqliteCommand, bool>(_ => throw new SqliteException("test", 1, 1)))
    );
  }
}
