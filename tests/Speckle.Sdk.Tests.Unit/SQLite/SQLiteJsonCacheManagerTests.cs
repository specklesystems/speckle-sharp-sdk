using FluentAssertions;
using Microsoft.Data.Sqlite;
using Speckle.Sdk.Common;
using Speckle.Sdk.SQLite;
using Xunit;

namespace Speckle.Sdk.Tests.Unit.SQLite;

public class SQLiteJsonCacheManagerTests : IDisposable
{
  private readonly string _basePath = $"{Guid.NewGuid()}.db";
  private readonly string? _connectionString;

  public SQLiteJsonCacheManagerTests() => _connectionString = $"Data Source={_basePath};";

  public void Dispose()
  {
    if (File.Exists(_basePath))
    {
      SqliteConnection.ClearAllPools();
      GC.Collect();
      GC.WaitForPendingFinalizers();
      File.Delete(_basePath);
    }
  }

  [Fact]
  public void TestGetAll()
  {
    var data = new List<(string id, string json)>() { ("id1", "1"), ("id2", "2") };
    using var manager = new SqLiteJsonCacheManager(_connectionString.NotNull(), 2);
    manager.SaveObjects(data);
    var items = manager.GetAllObjects();
    items.Count.Should().Be(data.Count);
    var i = items.ToDictionary();
    foreach (var (id, json) in data)
    {
      i.TryGetValue(id, out var j).Should().BeTrue();
      j.Should().Be(json);
    }
  }

  [Fact]
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
    items.Count.Should().Be(data.Count);

    var id1 = data[0].id;
    var json1 = manager.GetObject(id1);
    json1.Should().Be(data[0].json);
    manager.HasObject(id1).Should().BeTrue();

    manager.UpdateObject(id1, "3");
    json1 = manager.GetObject(id1);
    json1.Should().Be("3");
    manager.HasObject(id1).Should().BeTrue();

    manager.DeleteObject(id1);
    json1 = manager.GetObject(id1);
    json1.Should().BeNull();
    manager.HasObject(id1).Should().BeFalse();

    manager.UpdateObject(id1, "3");
    json1 = manager.GetObject(id1);
    json1.Should().Be("3");
    manager.HasObject(id1).Should().BeTrue();

    var id2 = data[1].id;
    var json2 = manager.GetObject(id2);
    json2.Should().Be(data[1].json);
    manager.HasObject(id2).Should().BeTrue();
    manager.DeleteObject(id2);
    json2 = manager.GetObject(id2);
    json2.Should().BeNull();
    manager.HasObject(id2).Should().BeFalse();
  }
}
