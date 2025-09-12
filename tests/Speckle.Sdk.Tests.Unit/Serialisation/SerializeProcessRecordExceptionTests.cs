using Microsoft.Extensions.Logging;
using Moq;
using Speckle.Sdk.Serialisation.V2;
using Speckle.Sdk.Serialisation.V2.Send;
using Speckle.Sdk.Testing;

namespace Speckle.Sdk.Tests.Unit.Serialisation;

public class SerializeProcessRecordExceptionTests : MoqTest
{
  [Fact]
  public async Task RecordException_LogsAndCancels_OnException()
  {
    // Arrange
    var loggerMock = Create<ILogger<SerializeProcess>>(MockBehavior.Loose);
    var loggerFactoryMock = Create<ILoggerFactory>();
    loggerFactoryMock
      .Setup(f => f.CreateLogger("Speckle.Sdk.Serialisation.V2.Send.SerializeProcess"))
      .Returns(loggerMock.Object);
    loggerFactoryMock
      .Setup(f => f.CreateLogger("Speckle.Sdk.Serialisation.V2.PriorityScheduler"))
      .Returns(Create<ILogger<PriorityScheduler>>().Object);
    var objectSaverMock = Create<IObjectSaver>();
    objectSaverMock.Setup(x => x.Dispose());
    var baseChildFinderMock = Create<IBaseChildFinder>();
    var baseSerializerMock = Create<IBaseSerializer>();
    using var cts = new CancellationTokenSource();
    await using var process = new SerializeProcess(
      null,
      objectSaverMock.Object,
      baseChildFinderMock.Object,
      baseSerializerMock.Object,
      loggerFactoryMock.Object,
      new(),
      cts.Token
    );
    var ex = new Exception("Test error");

    objectSaverMock.SetupSet(x => x.Exception = It.IsAny<Exception>()).Verifiable();

    // Act
    process.RecordException(ex);

    // Assert
    objectSaverMock.VerifySet(x => x.Exception = ex, Times.Once);
  }

  [Fact]
  public async Task RecordException_Ignores_OperationCanceledException()
  {
    // Arrange
    var loggerMock = Create<ILogger<SerializeProcess>>(MockBehavior.Loose);
    var loggerFactoryMock = Create<ILoggerFactory>();
    loggerFactoryMock
      .Setup(f => f.CreateLogger("Speckle.Sdk.Serialisation.V2.Send.SerializeProcess"))
      .Returns(loggerMock.Object);
    loggerFactoryMock
      .Setup(f => f.CreateLogger("Speckle.Sdk.Serialisation.V2.PriorityScheduler"))
      .Returns(Create<ILogger<PriorityScheduler>>().Object);
    var objectSaverMock = Create<IObjectSaver>();
    objectSaverMock.Setup(x => x.Dispose());
    var baseChildFinderMock = Create<IBaseChildFinder>();
    var baseSerializerMock = Create<IBaseSerializer>();
    using var cts = new CancellationTokenSource();
    await using var process = new SerializeProcess(
      null,
      objectSaverMock.Object,
      baseChildFinderMock.Object,
      baseSerializerMock.Object,
      loggerFactoryMock.Object,
      new(),
      cts.Token
    );
    var ex = new OperationCanceledException();

    // Act
    process.RecordException(ex);
  }

  [Fact]
  public async Task RecordException_Ignores_AggregateWithOnlyOperationCanceledException()
  {
    // Arrange
    var loggerMock = Create<ILogger<SerializeProcess>>(MockBehavior.Loose);
    var loggerFactoryMock = Create<ILoggerFactory>();
    loggerFactoryMock
      .Setup(f => f.CreateLogger("Speckle.Sdk.Serialisation.V2.Send.SerializeProcess"))
      .Returns(loggerMock.Object);
    loggerFactoryMock
      .Setup(f => f.CreateLogger("Speckle.Sdk.Serialisation.V2.PriorityScheduler"))
      .Returns(Create<ILogger<PriorityScheduler>>().Object);
    var objectSaverMock = Create<IObjectSaver>();
    objectSaverMock.Setup(x => x.Dispose());
    var baseChildFinderMock = Create<IBaseChildFinder>();
    var baseSerializerMock = Create<IBaseSerializer>();
    using var cts = new CancellationTokenSource();
    await using var process = new SerializeProcess(
      null,
      objectSaverMock.Object,
      baseChildFinderMock.Object,
      baseSerializerMock.Object,
      loggerFactoryMock.Object,
      new(),
      cts.Token
    );
    var ex = new AggregateException(new OperationCanceledException());

    // Act
    process.RecordException(ex);
  }
}
