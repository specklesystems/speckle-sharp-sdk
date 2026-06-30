using AwesomeAssertions;
using Speckle.Sdk.SQLite;

namespace Speckle.Sdk.Tests.Unit.SQLite;

public class SQLiteJsonCacheManagerTests : IDisposable
{
  private readonly string _basePath = $"{Guid.NewGuid()}.db";

  public void Dispose()
  {
    if (File.Exists(_basePath))
    {
      //don't disable the pool because we should be disabling it in the manager
      GC.Collect();
      GC.WaitForPendingFinalizers();
      File.Delete(_basePath);
    }
  }

  [Fact]
  public void TestGetAll()
  {
    var data = new List<(string id, string json)>() { ("id1", "1"), ("id2", "2") };
    using var manager = SqLiteJsonCacheManager.FromFilePath(_basePath, 2);
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
    using var manager = SqLiteJsonCacheManager.FromFilePath(_basePath, 2);
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

  [Fact]
  public void TestLargeJsonPayload()
  {
    var largeJson = new string('a', 100_000);
    using var manager = SqLiteJsonCacheManager.FromFilePath(_basePath, 2);
    manager.SaveObject("large", largeJson);
    var result = manager.GetObject("large");
    result.Should().Be(largeJson);
  }

  [Fact]
  public void TestSpecialCharactersInIdAndJson()
  {
    var id = "spécial_字符_!@#$%^&*()";
    var json = /*lang=json,strict*/
      "{\"value\": \"特殊字符!@#$%^&*()\"}";
    using var manager = SqLiteJsonCacheManager.FromFilePath(_basePath, 2);
    manager.SaveObject(id, json);
    var result = manager.GetObject(id);
    result.Should().Be(json);
    manager.HasObject(id).Should().BeTrue();
    manager.DeleteObject(id);
    manager.HasObject(id).Should().BeFalse();
  }

  [Fact]
  public void TestBulkInsertEmptyCollection()
  {
    using var manager = SqLiteJsonCacheManager.FromFilePath(_basePath, 2);
    manager.SaveObjects(new List<(string, string)>());
    manager.GetAllObjects().Count.Should().Be(0);
  }

  [Fact]
  public void TestRepeatedUpdateAndDelete()
  {
    using var manager = SqLiteJsonCacheManager.FromFilePath(_basePath, 2);
    manager.SaveObject("id", "1");
    manager.UpdateObject("id", "2");
    manager.UpdateObject("id", "3");
    manager.GetObject("id").Should().Be("3");
    manager.DeleteObject("id");
    manager.DeleteObject("id"); // Should not throw
    manager.GetObject("id").Should().BeNull();
  }

  [Fact]
  public void TestGetAndDeleteNonExistentId()
  {
    using var manager = SqLiteJsonCacheManager.FromFilePath(_basePath, 2);
    manager.GetObject("doesnotexist").Should().BeNull();
    manager.HasObject("doesnotexist").Should().BeFalse();
    manager.DeleteObject("doesnotexist"); // Should not throw
  }

  [Fact]
  public void TestNullOrEmptyInput()
  {
    using var manager = SqLiteJsonCacheManager.FromFilePath(_basePath, 2);
    // Empty id
    Assert.Throws<ArgumentException>(() => manager.SaveObject("", "emptyid"));
    // Empty json
    Assert.Throws<ArgumentException>(() => manager.SaveObject("eid", ""));
    // Null id/json (should throw)
    Assert.Throws<ArgumentNullException>(() => manager.SaveObject(null!, "json"));
    Assert.Throws<ArgumentNullException>(() => manager.SaveObject("nid", null!));
  }
}
