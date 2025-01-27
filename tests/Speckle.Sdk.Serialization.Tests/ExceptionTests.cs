using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.V2.Receive;
using Speckle.Sdk.Serialisation.V2.Send;
using Speckle.Sdk.Serialization.Tests.Framework;
using Speckle.Sdk.Testing.Framework;

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
    await Verify(ex).ScrubInternalizedStacktrace();
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
    await Verify(ex).ScrubInternalizedStacktrace();
  }

  [Theory]
  [InlineData("RevitObject.json.gz", "3416d3fe01c9196115514c4a2f41617b", 7818)]
  public async Task Test_Exceptions_Receive_Server(string fileName, string rootId, int oldCount)
  {
    var closures = await TestFileManager.GetFileAsClosures(fileName);
    closures.Count.Should().Be(oldCount);

    var o = new ObjectLoader(new DummySqLiteReceiveManager(closures), new ExceptionServerObjectManager(), null);
    using var process = new DeserializeProcess(
      null,
      o,
      new ObjectDeserializerFactory(),
      default,
      new(true, MaxParallelism: 1)
    );

    var ex = await Assert.ThrowsAsync<NotImplementedException>(async () =>
    {
      var root = await process.Deserialize(rootId);
    });
    await Verify(ex).ScrubInternalizedStacktrace();
  }

  [Theory]
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
    using var process = new DeserializeProcess(
      null,
      o,
      new ObjectDeserializerFactory(),
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
      ex = await Assert.ThrowsAsync<AggregateException>(async () =>
      {
        var root = await process.Deserialize(rootId);
      });
    }

    await Verify(ex).ScrubInternalizedStacktrace().UseParameters(hasObject);
  }
}
