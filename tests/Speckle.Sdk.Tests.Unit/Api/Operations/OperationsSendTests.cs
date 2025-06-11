#pragma warning disable IDE0005 // Using directive is unnecessary.
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Speckle.Sdk.Api;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.V2;
using Speckle.Sdk.Serialisation.V2.Send;
using Speckle.Sdk.Testing; // Added for MoqTest
using Speckle.Sdk.Transports;
using Xunit;
#pragma warning restore IDE0005

namespace Speckle.Sdk.Tests.Unit.Api.Operations;

public class OperationsSendTests : MoqTest
{
  public OperationsSendTests()
    : base()
  {
    // Constructor for xUnit, ensure it's empty or primarily for base calls
  }

  [Fact]
  public async Task Send2_ThrowsException_WhenChannelErrors()
  {
    // Arrange
    var mockSerializeProcess = Create<ISerializeProcess>(); // Changed from Mocker.Get
    var mockSerializeProcessFactory = Create<ISerializeProcessFactory>(); // Changed from Mocker.Get

    mockSerializeProcess
      .Setup(x => x.Serialize(It.IsAny<Base>()))
      .ThrowsAsync(new SpeckleException("Simulated channel error"));
    mockSerializeProcess.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask); // Added setup for DisposeAsync

    mockSerializeProcessFactory
      .Setup(x =>
        x.CreateSerializeProcess(
          It.IsAny<Uri>(),
          It.IsAny<string>(),
          It.IsAny<string>(), // authorizationToken
          It.IsAny<IProgress<ProgressArgs>>(), // onProgressAction
          It.IsAny<CancellationToken>(), // cancellationToken
          It.IsAny<SerializeProcessOptions>() // options
        )
      )
      .Returns(mockSerializeProcess.Object);

    // Setup other necessary mocks for Operations constructor
    // For mocks that are only used for their .Object property and don't need specific setups/verifications in this test:
    var mockActivityFactory = Create<ISdkActivityFactory>();
    var mockMetricsFactory = Create<ISdkMetricsFactory>();
    var mockActivity = Create<ISdkActivity>(); // Mock for what Start returns
    var mockCounter = Create<ISdkCounter<long>>(); // Mock for what CreateCounter returns
    var mockLogger = Create<ILogger<Speckle.Sdk.Api.Operations>>();
    var mockDeserializeProcessFactory = Create<IDeserializeProcessFactory>();

    mockActivityFactory.Setup(x => x.Start(It.IsAny<string>(), It.IsAny<string>())).Returns(mockActivity.Object);
    mockActivity.Setup(x => x.Dispose()); // Added setup for Dispose
    mockActivity.Setup(x => x.SetStatus(It.IsAny<SdkActivityStatusCode>())); // Added setup for SetStatus
    mockActivity.Setup(x => x.RecordException(It.IsAny<Exception>())); // Added setup for RecordException
    mockActivity.Setup(x => x.SetTag(It.IsAny<string>(), It.IsAny<object>())); // Added setup for SetTag

    mockMetricsFactory
      .Setup(x => x.CreateCounter<long>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
      .Returns(mockCounter.Object);
    mockCounter.Setup(x => x.Add(It.IsAny<long>())); // Added setup for the counter's Add method

    var operations = new Speckle.Sdk.Api.Operations(
      mockLogger.Object,
      mockActivityFactory.Object,
      mockMetricsFactory.Object,
      mockSerializeProcessFactory.Object,
      mockDeserializeProcessFactory.Object
    );

    var baseObject = new Base();
    var uri = new Uri("https://example.com");

    // Act & Assert
    var ex = await Assert.ThrowsAsync<SpeckleException>(async () =>
      await operations.Send2(
        uri,
        "streamId",
        "token",
        baseObject,
        null, // Progress<ProgressArgs>
        CancellationToken.None
      )
    );

    Assert.Equal("Simulated channel error", ex.Message);

    // Verify mocks
    mockSerializeProcess.Verify(x => x.Serialize(baseObject), Times.Once); // Changed from Mocker.Verify
    mockSerializeProcess.Verify(x => x.DisposeAsync(), Times.Once); // Changed from Mocker.Verify
  }
}
