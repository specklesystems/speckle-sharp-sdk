using Microsoft.Data.Sqlite;
using NUnit.Framework;
using Shouldly;
using Speckle.Sdk.Common;
using Speckle.Sdk.SQLite;

namespace Speckle.Sdk.Tests.Unit.SQLite;

[TestFixture]
public class SQLiteJsonCacheManagerTests
{
  private readonly string _basePath = $"{Guid.NewGuid()}.db";
  private string? _connectionString;

  [SetUp]
  public void Setup() => _connectionString = $"Data Source={_basePath};";

  [TearDown]
  public void TearDown()
  {
    if (File.Exists(_basePath))
    {
      SqliteConnection.ClearAllPools();
      GC.Collect();
      GC.WaitForPendingFinalizers();
      File.Delete(_basePath);
    }
  }

  [Test]
  public void TestGetAll()
  {
    var data = new List<(string id, string json)>() { ("id1", "1"), ("id2", "2") };
    using var manager = new SqLiteJsonCacheManager(_connectionString.NotNull(), 2);
    manager.SaveObjects(data);
    var items = manager.GetAllObjects();
    items.Count.ShouldBe(data.Count);
    var i = items.ToDictionary();
    foreach (var (id, json) in data)
    {
      i.TryGetValue(id, out var j).ShouldBeTrue();
      j.ShouldBe(json);
    }
  }

  [Test]
  public void TestGet()
  {
    var data = new List<(string id, string json)>() { ("id1", "1"), ("id2", "2") };
    using var manager = new SqLiteJsonCacheManager(_connectionString.NotNull(), 2);
    foreach (var d in data)
    {
      manager.SaveObject(d.id, d.json);
    }
    foreach (var d in data)
    {
      manager.SaveObject(d.id, d.json);
    }
    var items = manager.GetAllObjects();
    items.Count.ShouldBe(data.Count);

    var id1 = data[0].id;
    var json1 = manager.GetObject(id1);
    json1.ShouldBe(data[0].json);
    manager.HasObject(id1).ShouldBeTrue();

    manager.UpdateObject(id1, "3");
    json1 = manager.GetObject(id1);
    json1.ShouldBe("3");
    manager.HasObject(id1).ShouldBeTrue();

    manager.DeleteObject(id1);
    json1 = manager.GetObject(id1);
    json1.ShouldBeNull();
    manager.HasObject(id1).ShouldBeFalse();

    manager.UpdateObject(id1, "3");
    json1 = manager.GetObject(id1);
    json1.ShouldBe("3");
    manager.HasObject(id1).ShouldBeTrue();

    var id2 = data[1].id;
    var json2 = manager.GetObject(id2);
    json2.ShouldBe(data[1].json);
    manager.HasObject(id2).ShouldBeTrue();
    manager.DeleteObject(id2);
    json2 = manager.GetObject(id2);
    json2.ShouldBeNull();
    manager.HasObject(id2).ShouldBeFalse();
  }
}
