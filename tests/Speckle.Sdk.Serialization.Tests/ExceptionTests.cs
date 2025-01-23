using Microsoft.Extensions.Logging.Abstractions;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.V2;
using Speckle.Sdk.Serialisation.V2.Send;
using Speckle.Sdk.SQLite;
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
      new SerializeProcessOptions(false, false, false, true)
    );

    //4 exceptions are fine because we use 4 threads for saving cache
    var ex = await Assert.ThrowsAsync<AggregateException>(async () => await process2.Serialize(testClass, default));
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
      new SerializeProcessOptions(false, false, false, true)
    );

    var ex = await Assert.ThrowsAsync<AggregateException>(async () => await process2.Serialize(testClass, default));
    await Verify(ex);
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

public class ExceptionSendCacheManager : ISqLiteJsonCacheManager
{
  public void Dispose() { }

  public IReadOnlyCollection<(string Id, string Json)> GetAllObjects() => throw new NotImplementedException();

  public void DeleteObject(string id) => throw new NotImplementedException();

  public string? GetObject(string id) => null;

  public void SaveObject(string id, string json) => throw new NotImplementedException();

  public void UpdateObject(string id, string json) => throw new NotImplementedException();

  public void SaveObjects(IEnumerable<(string id, string json)> items) => throw new NotImplementedException();

  public bool HasObject(string objectId) => throw new NotImplementedException();
}
