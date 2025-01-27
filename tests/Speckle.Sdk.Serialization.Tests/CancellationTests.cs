using Microsoft.Extensions.Logging.Abstractions;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.V2.Send;
using Speckle.Sdk.Testing.Framework;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialization.Tests;

public class CancellationSqLiteSendManager(CancellationTokenSource cancellationTokenSource) : DummySqLiteSendManager
{
  public override void SaveObjects(IEnumerable<(string id, string json)> items)
  {
    cancellationTokenSource.Cancel();
    cancellationTokenSource.Token.ThrowIfCancellationRequested();
  }
}

public class CancellationServerObjectManager(CancellationTokenSource cancellationTokenSource) : DummyServerObjectManager
{
  public override Task UploadObjects(
    IReadOnlyList<BaseItem> objects,
    bool compressPayloads,
    IProgress<ProgressArgs>? progress,
    CancellationToken cancellationToken
  )
  {
    cancellationTokenSource.Cancel();
    cancellationTokenSource.Token.ThrowIfCancellationRequested();
    return base.UploadObjects(objects, compressPayloads, progress, cancellationToken);
  }
}

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
  }
}
