using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Serialisation.V2;
using Speckle.Sdk.Serialisation.V2.Receive;
using Speckle.Sdk.Serialisation.V2.Send;
using Speckle.Sdk.Serialization.Tests.Framework;
using Speckle.Sdk.SQLite;
using Speckle.Sdk.Testing.Framework;

namespace Speckle.Sdk.Serialization.Tests;

public class ExceptionTests : IDisposable
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

  private readonly ISerializeProcessFactory _factory;

  public ExceptionTests()
  {
    var serviceCollection = new ServiceCollection();
    serviceCollection.AddSpeckleSdk(new("Tests", "test"), "v3", typeof(TestClass).Assembly, typeof(Polyline).Assembly);
    var serviceProvider = serviceCollection.BuildServiceProvider();

    _factory = serviceProvider.GetRequiredService<ISerializeProcessFactory>();
  }

  [Fact]
  public async Task Test_Exceptions_Upload()
  {
    var testClass = new TestClass() { RegularProperty = "Hello" };

    var objects = new ConcurrentDictionary<Id, Json>();

    await using var serializeProcess = _factory.CreateSerializeProcess(
      new MemoryJsonCacheManager(objects),
      new ExceptionServerObjectManager(),
      null,
      default,
      new SerializeProcessOptions(false, false, false, true)
    );

    //4 exceptions are fine because we use 4 threads for saving cache
    var ex = await Assert.ThrowsAsync<SpeckleException>(async () => await serializeProcess.Serialize(testClass));
    await Verify(ex);
  }

  [Fact]
  public async Task Test_Exceptions_Cache()
  {
    var testClass = new TestClass() { RegularProperty = "Hello" };

    await using var serializeProcess = _factory.CreateSerializeProcess(
      new ExceptionSendCacheManager(),
      new MemoryServerObjectManager(new()),
      null,
      default,
      new SerializeProcessOptions(false, false, false, true)
    );

    var ex = await Assert.ThrowsAsync<SpeckleException>(async () => await serializeProcess.Serialize(testClass));
    await Verify(ex);
  }

  [Theory]
  [InlineData(2000, 0, true)]
  [InlineData(0, 5000, false)]
  public async Task Test_Database_Locked(int delayMilliseconds, int timeoutMilliseconds, bool throws)
  {
    var @base = new SampleObjectBase2();
    @base["dynamicProp"] = 123;
    @base.applicationId = "1";
    @base.detachedProp = new SamplePropBase2()
    {
      name = "detachedProp",
      applicationId = "2",
      line = new Polyline() { units = "test", value = [1.0, 2.0] },
    };
    @base.detachedProp2 = new SamplePropBase2()
    {
      name = "detachedProp2",
      applicationId = "3",
      line = new Polyline() { units = "test", value = [3.0, 2.0] },
    };
    @base.attachedProp = new SamplePropBase2()
    {
      name = "attachedProp",
      applicationId = "4",
      line = new Polyline() { units = "test", value = [3.0, 4.0] },
    };

    CacheDbCommandPool.UseDelayTimeSpan = TimeSpan.FromMilliseconds(delayMilliseconds);
    CacheDbCommandPool.ExceptionOccurred = ex => throw ex;
    SqLiteJsonCacheManager.SqliteWaitTimeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);

    if (throws)
    {
      SpeckleException? ex = null;
      for (int i = 0; i < 10; i++)
      {
        Console.WriteLine("Times " + i);
        try
        {
          await using var serializeProcess = _factory.CreateSerializeProcess(
            SqLiteJsonCacheManager.FromFilePath(_basePath, SqLiteJsonCacheManagerFactory.INITIAL_CONCURRENCY),
            new MemoryServerObjectManager(new()),
            null,
            default,
            new SerializeProcessOptions(false, false, false, true) { MaxCacheBatchSize = 1 }
          );
          await serializeProcess.Serialize(@base);
        }
        catch (SpeckleException se)
        {
          ex = se;
        }

        if (ex is null)
        {
          continue;
        }

        await Verify(ex);
        break;
      }
    }
    else
    {
      await using var serializeProcess = _factory.CreateSerializeProcess(
        SqLiteJsonCacheManager.FromFilePath(_basePath, SqLiteJsonCacheManagerFactory.INITIAL_CONCURRENCY),
        new MemoryServerObjectManager(new()),
        null,
        default,
        new SerializeProcessOptions(false, false, false, true) { MaxCacheBatchSize = 1 }
      );
      var results = await serializeProcess.Serialize(@base);
      results.RootId.Should().Be(@base.id);
    }
  }

  [Fact]
  public async Task Test_Exceptions_Cache_ExceptionsAfter_10()
  {
    var @base = new SampleObjectBase2();
    @base["dynamicProp"] = 123;
    @base.applicationId = "1";
    @base.detachedProp = new SamplePropBase2()
    {
      name = "detachedProp",
      applicationId = "2",
      line = new Polyline() { units = "test", value = [1.0, 2.0] },
    };
    @base.detachedProp2 = new SamplePropBase2()
    {
      name = "detachedProp2",
      applicationId = "3",
      line = new Polyline() { units = "test", value = [3.0, 2.0] },
    };
    @base.attachedProp = new SamplePropBase2()
    {
      name = "attachedProp",
      applicationId = "4",
      line = new Polyline() { units = "test", value = [3.0, 4.0] },
    };

    await using var serializeProcess = _factory.CreateSerializeProcess(
      new ExceptionSendCacheManager(exceptionsAfter: 10),
      new MemoryServerObjectManager(new()),
      null,
      default,
      new SerializeProcessOptions(false, false, false, true)
      {
        MaxHttpSendBatchSize = 1,
        MaxCacheBatchSize = 1,
        MaxParallelism = 1,
      }
    );

    var ex = await Assert.ThrowsAsync<SpeckleException>(async () => await serializeProcess.Serialize(@base));
    await Verify(ex);
  }

  [Fact]
  public async Task Test_Exceptions_Receive_Server_Skip_Both()
  {
    var o = new ObjectLoader(
      new DummySqLiteReceiveManager(new Dictionary<string, string>()),
      new ExceptionServerObjectManager(),
      null,
      new NullLogger<ObjectLoader>(),
      default
    );
    await using var process = new DeserializeProcess(
      o,
      null,
      new BaseDeserializer(new ObjectDeserializerFactory()),
      new NullLoggerFactory(),
      default,
      new(SkipCache: true, MaxParallelism: 1, SkipServer: true)
    );

    var ex = await Assert.ThrowsAsync<SpeckleException>(async () =>
    {
      var root = await process.Deserialize(Guid.NewGuid().ToString());
    });
    await Verify(ex);
  }

  [Theory]
  [InlineData("RevitObject.json.gz", "3416d3fe01c9196115514c4a2f41617b", 7818)]
  public async Task Test_Exceptions_Receive_Server(string fileName, string rootId, int oldCount)
  {
    var closures = TestFileManager.GetFileAsClosures(fileName);
    closures.Count.Should().Be(oldCount);

    await using var process = new DeserializeProcess(
      new DummySqLiteReceiveManager(closures),
      new ExceptionServerObjectManager(),
      null,
      new BaseDeserializer(new ObjectDeserializerFactory()),
      new NullLoggerFactory(),
      default,
      new(true, MaxParallelism: 1)
    );

    var ex = await Assert.ThrowsAsync<NotImplementedException>(async () =>
    {
      var root = await process.Deserialize(rootId);
    });
    await Verify(ex);
  }

  [Theory]
  [InlineData("RevitObject.json.gz", "3416d3fe01c9196115514c4a2f41617b", 7818, false)]
  [InlineData("RevitObject.json.gz", "3416d3fe01c9196115514c4a2f41617b", 7818, true)]
  public async Task Test_Exceptions_Receive_Cache(string fileName, string rootId, int oldCount, bool? hasObject)
  {
    var closures = TestFileManager.GetFileAsClosures(fileName);
    closures.Count.Should().Be(oldCount);

    await using var process = new DeserializeProcess(
      new ExceptionSendCacheManager(hasObject),
      new DummyReceiveServerObjectManager(closures),
      null,
      new BaseDeserializer(new ObjectDeserializerFactory()),
      new NullLoggerFactory(),
      default,
      new(MaxParallelism: 1)
    );

    Exception ex;
    if (hasObject == true)
    {
      ex = await Assert.ThrowsAsync<NotImplementedException>(async () =>
      {
        var root = await process.Deserialize(rootId);
      });
    }
    else
    {
      ex = await Assert.ThrowsAsync<SpeckleException>(async () =>
      {
        var root = await process.Deserialize(rootId);
      });
    }
    await Verify(ex).UseParameters(hasObject);
  }

  [SpeckleType("Objects.Geometry.BadBase")]
  public class BadBase : Base
  {
#pragma warning disable CA1065
    public string BadProp => throw new NotImplementedException();
#pragma warning restore CA1065
  }

  [Fact]
  public void Test_SpeckleSerializerException()
  {
    var factory = new ObjectSerializerFactory(new BasePropertyGatherer());
    var serializer = factory.Create(new Dictionary<Id, NodeInfo>(), default);
    Assert.Throws<SpeckleSerializeException>(() =>
    {
      var _ = serializer.Serialize(new BadBase()).ToList();
    });
  }
}
