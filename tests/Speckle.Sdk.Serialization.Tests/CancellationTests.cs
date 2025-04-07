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
using Speckle.Sdk.Testing.Framework;

namespace Speckle.Sdk.Serialization.Tests;

public class CancellationTests
{
  private readonly ISerializeProcessFactory _factory;

  public CancellationTests()
  {
    var serviceCollection = new ServiceCollection();
    serviceCollection.AddSpeckleSdk("Tests", "test", "v3", typeof(TestClass).Assembly, typeof(Polyline).Assembly);
    var serviceProvider = serviceCollection.BuildServiceProvider();

    _factory = serviceProvider.GetRequiredService<ISerializeProcessFactory>();
  }

  [Fact]
  public async Task Cancellation_Serialize()
  {
    var testClass = new TestClass() { RegularProperty = "Hello" };

    using var cancellationSource = new CancellationTokenSource();

    await using var serializeProcess = _factory.CreateSerializeProcess(
      new ConcurrentDictionary<Id, Json>(),
      new ConcurrentDictionary<string, string>(),
      null,
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

    await using var serializeProcess = _factory.CreateSerializeProcess(
      new DummySqLiteSendManager(),
      new CancellationServerObjectManager(cancellationSource),
      null,
      cancellationSource.Token,
      new SerializeProcessOptions(true, true, false, true)
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
    await using var serializeProcess = _factory.CreateSerializeProcess(
      new DummySqLiteSendManager(),
      new CancellationServerObjectManager(cancellationSource),
      null,
      cancellationSource.Token,
      new SerializeProcessOptions(true, true, false, true)
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
    var closures = TestFileManager.GetFileAsClosures(fileName);
    closures.Count.Should().Be(oldCount);

    using var cancellationSource = new CancellationTokenSource();
    await using var process = new DeserializeProcess(
      new CancellationSqLiteJsonCacheManager(cancellationSource),
      new DummyReceiveServerObjectManager(closures),
      null,
      new BaseDeserializer(new ObjectDeserializerFactory()),
      new NullLoggerFactory(),
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

  [Theory]
  [InlineData("RevitObject.json.gz", "3416d3fe01c9196115514c4a2f41617b", 7818)]
  public async Task Cancellation_Receive_Server(string fileName, string rootId, int oldCount)
  {
    var closures = TestFileManager.GetFileAsClosures(fileName);
    closures.Count.Should().Be(oldCount);

    using var cancellationSource = new CancellationTokenSource();
    await using var process = new DeserializeProcess(
      new DummyCancellationSqLiteSendManager(),
      new CancellationServerObjectManager(cancellationSource),
      null,
      new BaseDeserializer(new ObjectDeserializerFactory()),
      new NullLoggerFactory(),
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

  [Theory]
  [InlineData("RevitObject.json.gz", "3416d3fe01c9196115514c4a2f41617b", 7818)]
  public async Task Cancellation_Receive_Deserialize(string fileName, string rootId, int oldCount)
  {
    var closures = TestFileManager.GetFileAsClosures(fileName);
    closures.Count.Should().Be(oldCount);

    using var cancellationSource = new CancellationTokenSource();
    await using var process = new DeserializeProcess(
      new DummySqLiteReceiveManager(closures),
      new DummyReceiveServerObjectManager(closures),
      null,
      new CancellationBaseDeserializer(cancellationSource),
      new NullLoggerFactory(),
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

public class CancellationBaseDeserializer(CancellationTokenSource cancellationTokenSource) : IBaseDeserializer
{
  public Base Deserialise(
    ConcurrentDictionary<Id, Base> baseCache,
    Id id,
    Json json,
    IReadOnlyCollection<Id> closures,
    CancellationToken cancellationToken
  )
  {
    cancellationTokenSource.Cancel();
    cancellationTokenSource.Token.ThrowIfCancellationRequested();
    throw new NotImplementedException();
  }
}
