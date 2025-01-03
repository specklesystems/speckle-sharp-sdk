using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;
using Shouldly;
using Speckle.Sdk.Common;
using Speckle.Sdk.Transports;
using Xunit;

namespace Speckle.Sdk.Tests.Unit.Transports;

public sealed class SQLiteTransportTests : TransportTests, IDisposable
{
  protected override ITransport? Sut => _sqlite;

  private SQLiteTransport _sqlite;

  private static readonly string s_basePath = $"./temp {Guid.NewGuid()}";
  private const string APPLICATION_NAME = "Speckle Integration Tests";

  // Constructor replaces [SetUp]
  public SQLiteTransportTests()
  {
    _sqlite = new SQLiteTransport(s_basePath, APPLICATION_NAME);
  }

  // Disposal replaces [TearDown] for cleanup
  public void Dispose()
  {
    _sqlite?.Dispose();
    SqliteConnection.ClearAllPools();
    Directory.Delete(s_basePath, true);
  }

  [Fact]
  public void DbCreated_AfterInitialization()
  {
    bool fileExists = File.Exists($"{s_basePath}/{APPLICATION_NAME}/Data.db");
    fileExists.ShouldBeTrue();
  }

  [Fact]
  public async Task UpdateObject_AfterAdd()
  {
    const string PAYLOAD_ID = "MyTestObjectId";
    const string PAYLOAD_DATA = "MyTestObjectData";

    _sqlite!.SaveObject(PAYLOAD_ID, PAYLOAD_DATA);
    await _sqlite.WriteComplete();

    const string NEW_PAYLOAD = "MyEvenBetterObjectData";
    _sqlite.UpdateObject(PAYLOAD_ID, NEW_PAYLOAD);
    await _sqlite.WriteComplete();

    var result = await _sqlite.GetObject(PAYLOAD_ID);
    result.ShouldBe(NEW_PAYLOAD);
  }

  [Fact]
  public async Task UpdateObject_WhenMissing()
  {
    const string PAYLOAD_ID = "MyTestObjectId";
    const string PAYLOAD_DATA = "MyTestObjectData";

    var preUpdate = await _sqlite!.GetObject(PAYLOAD_ID);
    preUpdate.ShouldBeNull();

    _sqlite.UpdateObject(PAYLOAD_ID, PAYLOAD_DATA);
    await _sqlite.WriteComplete();

    var postUpdate = await _sqlite.GetObject(PAYLOAD_ID);
    postUpdate.ShouldBe(PAYLOAD_DATA);
  }

  [Fact]
  public async Task SaveAndRetrieveObject_Sync()
  {
    const string PAYLOAD_ID = "MyTestObjectId";
    const string PAYLOAD_DATA = "MyTestObjectData";

    var preAdd = await Sut!.GetObject(PAYLOAD_ID);
    preAdd.ShouldBeNull();

    _sqlite!.SaveObjectSync(PAYLOAD_ID, PAYLOAD_DATA);

    {
      var postAdd = await Sut.GetObject(PAYLOAD_ID);
      postAdd.ShouldBe(PAYLOAD_DATA);
    }
  }

  [Fact] // No xUnit [Timeout], so this is purely indicative
  public void UpdateObject_WhileEnumerating()
  {
    const string UPDATE_STRING = "_new";
    Dictionary<string, string> testData = new()
    {
      { "a", "This is object a" },
      { "b", "This is object b" },
      { "c", "This is object c" },
      { "d", "This is object d" },
    };
    int length = testData.Values.First().Length;

    foreach (var (key, data) in testData)
    {
      _sqlite!.SaveObjectSync(key, data);
    }

    foreach (var o in _sqlite.GetAllObjects())
    {
      string newData = o + UPDATE_STRING;
      string key = $"{o[length - 1]}";

      _sqlite.UpdateObject(key, newData);
    }

    // Assert that objects were updated
    _sqlite.GetAllObjects().ToList().ShouldAllBe(o => o.Contains(UPDATE_STRING));
    // Assert that objects were only updated once
    _sqlite.GetAllObjects().ToList().ShouldAllBe(o => o.Length == length + UPDATE_STRING.Length);
  }

  [Theory]
  [InlineData(6, 32)]
  public void GetAllObjects_IsThreadSafe(int dataSize, int parallelism)
  {
    foreach (int i in Enumerable.Range(0, dataSize))
    {
      _sqlite!.SaveObjectSync(i.ToString(), Guid.NewGuid().ToString());
    }

    List<string>[] results = new List<string>[parallelism];
    Parallel.ForEach(
      Enumerable.Range(0, parallelism),
      i =>
      {
        results[i] = _sqlite.GetAllObjects().ToList();
      }
    );

    foreach (var result in results)
    {
      result.ShouldBeEquivalentTo(results[0]);
      result.Count.ShouldBe(dataSize);
    }
  }
}
