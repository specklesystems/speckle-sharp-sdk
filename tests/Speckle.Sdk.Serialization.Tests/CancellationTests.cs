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

public class CancellationTests
{
  public CancellationTests()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(DetachedTests).Assembly, typeof(Polyline).Assembly);
  }

  [Fact]
  public async Task Cancellation_Serialize()
  {
    var testClass = new TestClass() { RegularProperty = "Hello" };

    using var cancellationSource = new CancellationTokenSource();
    using var serializeProcess = new SerializeProcess(
      null,
      new DummySqLiteSendManager(),
      new DummyServerObjectManager(),
      new BaseChildFinder(new BasePropertyGatherer()),
      new BaseSerializer(new DummySqLiteSendManager(), new ObjectSerializerFactory(new BasePropertyGatherer())),
      new NullLoggerFactory(),
      cancellationSource.Token,
      new SerializeProcessOptions(true, true, false, true)
    );
    await cancellationSource.CancelAsync();
    var ex = await Assert.ThrowsAsync<OperationCanceledException>(
      async () => await serializeProcess.Serialize(testClass)
    );
    await Verify(ex);
    cancellationSource.IsCancellationRequested.Should().BeTrue();
  }

  [Fact]
  public async Task Cancellation_Save_Server()
  {
    var testClass = new TestClass() { RegularProperty = "Hello" };

    using var cancellationSource = new CancellationTokenSource();
    using var serializeProcess = new SerializeProcess(
      null,
      new DummySqLiteSendManager(),
      new CancellationServerObjectManager(cancellationSource),
      new BaseChildFinder(new BasePropertyGatherer()),
      new BaseSerializer(new DummySqLiteSendManager(), new ObjectSerializerFactory(new BasePropertyGatherer())),
      new NullLoggerFactory(),
      cancellationSource.Token,
      new SerializeProcessOptions(true, false, false, true)
    );
    var ex = await Assert.ThrowsAsync<OperationCanceledException>(
      async () => await serializeProcess.Serialize(testClass)
    );
    await Verify(ex);
    cancellationSource.IsCancellationRequested.Should().BeTrue();
  }

  [Fact]
  public async Task Cancellation_Save_Sqlite()
  {
    var testClass = new TestClass() { RegularProperty = "Hello" };

    using var cancellationSource = new CancellationTokenSource();
    using var serializeProcess = new SerializeProcess(
      null,
      new CancellationSqLiteSendManager(cancellationSource),
      new DummyServerObjectManager(),
      new BaseChildFinder(new BasePropertyGatherer()),
      new BaseSerializer(new DummySqLiteSendManager(), new ObjectSerializerFactory(new BasePropertyGatherer())),
      new NullLoggerFactory(),
      cancellationSource.Token,
      new SerializeProcessOptions(true, false, false, true)
    );
    var ex = await Assert.ThrowsAsync<OperationCanceledException>(
      async () => await serializeProcess.Serialize(testClass)
    );
    await Verify(ex);
    cancellationSource.IsCancellationRequested.Should().BeTrue();
  }

  [Theory]
  [InlineData("RevitObject.json.gz", "3416d3fe01c9196115514c4a2f41617b", 7818)]
  public async Task Cancellation_Receive_Cache(string fileName, string rootId, int oldCount)
  {
    var closures = await TestFileManager.GetFileAsClosures(fileName);
    closures.Count.Should().Be(oldCount);

    using var cancellationSource = new CancellationTokenSource();
    var o = new ObjectLoader(
      new CancellationSqLiteJsonCacheManager(cancellationSource),
      new DummyReceiveServerObjectManager(closures),
      null
    );
    using var process = new DeserializeProcess(
      null,
      o,
      new ObjectDeserializerFactory(),
      cancellationSource.Token,
      new(MaxParallelism: 1)
    );

    var ex = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
    {
      var root = await process.Deserialize(rootId);
    });

    await Verify(ex);
    cancellationSource.IsCancellationRequested.Should().BeTrue();
  }
}
