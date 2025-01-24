using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.V2;
using Speckle.Sdk.Serialisation.V2.Receive;
using Speckle.Sdk.Serialisation.V2.Send;
using Speckle.Sdk.SQLite;
using Speckle.Sdk.Testing.Framework;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialization.Tests;

public class ExceptionTests
{
  public ExceptionTests()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(DetachedTests).Assembly, typeof(Polyline).Assembly);
  }

  [Fact]
  public async Task Test_Exceptions_Upload()
  {
    var testClass = new TestClass() { RegularProperty = "Hello" };

    var objects = new Dictionary<string, string>();
    using var process2 = new SerializeProcess(
      null,
      new DummySendCacheManager(objects),
      new ExceptionServerObjectManager(),
      new BaseChildFinder(new BasePropertyGatherer()),
      new ObjectSerializerFactory(new BasePropertyGatherer()),
      new NullLoggerFactory(),
      default,
      new SerializeProcessOptions(false, false, false, true)
    );

    //4 exceptions are fine because we use 4 threads for saving cache
    var ex = await Assert.ThrowsAsync<AggregateException>(async () => await process2.Serialize(testClass));
    await Verify(ex);
  }

  [Fact]
  public async Task Test_Exceptions_Cache()
  {
    var testClass = new TestClass() { RegularProperty = "Hello" };

    using var process2 = new SerializeProcess(
      null,
      new ExceptionSendCacheManager(),
      new DummyServerObjectManager(),
      new BaseChildFinder(new BasePropertyGatherer()),
      new ObjectSerializerFactory(new BasePropertyGatherer()),
      new NullLoggerFactory(),
      default,
      new SerializeProcessOptions(false, false, false, true)
    );

    var ex = await Assert.ThrowsAsync<AggregateException>(async () => await process2.Serialize(testClass));
    await Verify(ex);
  }
  
  
  
  
  [Theory]
  [InlineData("RevitObject.json.gz", "3416d3fe01c9196115514c4a2f41617b", 7818)]
  public async Task Test_Exceptions_Receive_Server(string fileName, string rootId, int oldCount)
  {
    var closures = await TestFileManager.GetFileAsClosures(fileName);
    closures.Count.Should().Be(oldCount);

    var o = new ObjectLoader(
      new DummySqLiteReceiveManager(closures),
      new ExceptionServerObjectManager(),
      null
    );
    using var process = new DeserializeProcess(null, o, new ObjectDeserializerFactory(), new(true));


    var ex = await Assert.ThrowsAsync<NotImplementedException>(async () =>
    {
      var root = await process.Deserialize(rootId, default);
    });
    await Verify(ex);
  }
  
  [Theory]
  [InlineData("RevitObject.json.gz", "3416d3fe01c9196115514c4a2f41617b", 7818, null)]
  [InlineData("RevitObject.json.gz", "3416d3fe01c9196115514c4a2f41617b", 7818, false)]
  [InlineData("RevitObject.json.gz", "3416d3fe01c9196115514c4a2f41617b", 7818, true)]
  public async Task Test_Exceptions_Receive_Cache(string fileName, string rootId, int oldCount, bool? hasObject)
  {
    var closures = await TestFileManager.GetFileAsClosures(fileName);
    closures.Count.Should().Be(oldCount);

    var o = new ObjectLoader(
      new ExceptionSendCacheManager(hasObject),
      new DummyReceiveServerObjectManager(closures),
      null
    );
    using var process = new DeserializeProcess(null, o, new ObjectDeserializerFactory(), new(MaxParallelism: 2));

    Exception ex;
    if (hasObject == true) 
    {
      ex = await Assert.ThrowsAsync<NotImplementedException>(async () =>
      {
        var root = await process.Deserialize(rootId, default);
      });
    }
    else
    {
      ex = await Assert.ThrowsAsync<AggregateException>(async () =>
      {
        var root = await process.Deserialize(rootId, default);
      });
    }

    await Verify(ex).UseParameters(hasObject);
  }
}

public class ExceptionServerObjectManager : IServerObjectManager
{
  public IAsyncEnumerable<(string, string)> DownloadObjects(
    IReadOnlyCollection<string> objectIds,
    IProgress<ProgressArgs>? progress,
    CancellationToken cancellationToken
  ) => throw new NotImplementedException();

  public Task<string?> DownloadSingleObject(
    string objectId,
    IProgress<ProgressArgs>? progress,
    CancellationToken cancellationToken
  ) => throw new NotImplementedException();

  public Task<Dictionary<string, bool>> HasObjects(
    IReadOnlyCollection<string> objectIds,
    CancellationToken cancellationToken
  ) => throw new NotImplementedException();

  public Task UploadObjects(
    IReadOnlyList<BaseItem> objects,
    bool compressPayloads,
    IProgress<ProgressArgs>? progress,
    CancellationToken cancellationToken
  ) => throw new NotImplementedException();
}

public class ExceptionSendCacheManager(bool? hasObject = null) : ISqLiteJsonCacheManager
{
  public void Dispose() { }

  public IReadOnlyCollection<(string Id, string Json)> GetAllObjects() => throw new NotImplementedException();

  public void DeleteObject(string id) => throw new NotImplementedException();

  public string? GetObject(string id) => null;

  public void SaveObject(string id, string json) => throw new NotImplementedException();

  public void UpdateObject(string id, string json) => throw new NotImplementedException();

  public void SaveObjects(IEnumerable<(string id, string json)> items) => throw new NotImplementedException();

  public bool HasObject(string objectId) => hasObject ?? throw new NotImplementedException();
}
