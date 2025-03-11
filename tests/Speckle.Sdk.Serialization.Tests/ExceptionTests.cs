using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Serialisation.V2;
using Speckle.Sdk.Serialisation.V2.Receive;
using Speckle.Sdk.Serialisation.V2.Send;
using Speckle.Sdk.Serialization.Tests.Framework;
using Speckle.Sdk.Testing.Framework;

namespace Speckle.Sdk.Serialization.Tests;

public class ExceptionTests
{
  private readonly ISerializeProcessFactory _factory;

  public ExceptionTests()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(DetachedTests).Assembly, typeof(Polyline).Assembly);

    var serviceCollection = new ServiceCollection();
    serviceCollection.AddSpeckleSdk(HostApplications.Navisworks, HostAppVersion.v2023, "Test");
    var serviceProvider = serviceCollection.BuildServiceProvider();

    _factory = serviceProvider.GetRequiredService<ISerializeProcessFactory>();
  }

  [Fact]
  public async Task Test_Exceptions_Upload()
  {
    var testClass = new TestClass() { RegularProperty = "Hello" };

    var objects = new ConcurrentDictionary<string, string>();

    await using var serializeProcess = _factory.CreateSerializeProcess(
      new ConcurrentDictionary<Id, Json>(),
      objects,
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
      new ConcurrentDictionary<Id, Json>(),
      new ConcurrentDictionary<string, string>(),
      null,
      default,
      new SerializeProcessOptions(false, false, false, true)
    );

    var ex = await Assert.ThrowsAsync<SpeckleException>(async () => await serializeProcess.Serialize(testClass));
    await Verify(ex);
  }

  [Fact]
  public async Task Test_Exceptions_Receive_Server_Skip_Both()
  {
    var o = new ObjectLoader(
      new DummySqLiteReceiveManager(new Dictionary<string, string>()),
      new ExceptionServerObjectManager(),
      null,
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
    var closures = await TestFileManager.GetFileAsClosures(fileName);
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
    var closures = await TestFileManager.GetFileAsClosures(fileName);
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
