using Microsoft.Extensions.Logging.Abstractions;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.V2.Send;
using Speckle.Sdk.Testing.Framework;

namespace Speckle.Sdk.Serialization.Tests;

public class CancellationSqLiteSendManager : DummySqLiteSendManager
{
  
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
      new BaseChildFinder(new BasePropertyGatherer()),   new BaseSerializer(
        
        new DummySqLiteSendManager(),new ObjectSerializerFactory(new BasePropertyGatherer())),

      new NullLoggerFactory(),
      cancellationSource.Token,
      new SerializeProcessOptions(true, true, false, true)
    );
    await cancellationSource.CancelAsync();
    var ex = await Assert.ThrowsAsync<OperationCanceledException>(async () => await serializeProcess.Serialize(testClass));
    await Verify(ex);

  }
   
  [Fact]
  public async Task Cancellation_Save_Sqlite()
  {
    var testClass = new TestClass() { RegularProperty = "Hello" };
  
    using var cancellationSource = new CancellationTokenSource();
    using var serializeProcess = new SerializeProcess(
      null,
      new CancellationSqLiteSendManager(),
      new DummyServerObjectManager(),
      new BaseChildFinder(new BasePropertyGatherer()),   new BaseSerializer(
        
        new DummySqLiteSendManager(),new ObjectSerializerFactory(new BasePropertyGatherer())),

      new NullLoggerFactory(),
      cancellationSource.Token,
      new SerializeProcessOptions(true, true, false, true)
    );
    await cancellationSource.CancelAsync();
    var ex = await Assert.ThrowsAsync<OperationCanceledException>(async () => await serializeProcess.Serialize(testClass));
    await Verify(ex);

  }
}
