#pragma warning disable IDE0005 // Suppress unnecessary using directives for this file if they cause build issues
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit; // Changed from NUnit.Framework
using Speckle.Sdk.Api;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.V2;
using Speckle.Sdk.Serialisation.V2.Send;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Tests.Unit.Api.Operations;

// [TestFixture] // Removed
public class OperationsSendTests
{
    private Mock<ILogger<Speckle.Sdk.Api.Operations>> _loggerMock;
    private Mock<ISdkActivityFactory> _activityFactoryMock;
    private Mock<ISdkMetricsFactory> _metricsFactoryMock;
    private Mock<ISerializeProcessFactory> _serializeProcessFactoryMock;
    private Mock<IDeserializeProcessFactory> _deserializeProcessFactoryMock;
    private Speckle.Sdk.Api.Operations _operations;

    // [SetUp] // Changed to constructor
    public OperationsSendTests()
    {
        _loggerMock = new Mock<ILogger<Speckle.Sdk.Api.Operations>>();
        _activityFactoryMock = new Mock<ISdkActivityFactory>();
        _metricsFactoryMock = new Mock<ISdkMetricsFactory>();
        _serializeProcessFactoryMock = new Mock<ISerializeProcessFactory>();
        _deserializeProcessFactoryMock = new Mock<IDeserializeProcessFactory>();

        _activityFactoryMock.Setup(x => x.Start(It.IsAny<string>(), It.IsAny<string>())).Returns(new Mock<ISdkActivity>().Object);
        _metricsFactoryMock.Setup(x => x.CreateCounter<long>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(new Mock<ISdkCounter<long>>().Object);

        _operations = new Speckle.Sdk.Api.Operations(
            _loggerMock.Object,
            _activityFactoryMock.Object,
            _metricsFactoryMock.Object,
            _serializeProcessFactoryMock.Object,
            _deserializeProcessFactoryMock.Object
        );
    }

    [Fact] // Changed from [Test]
    public async Task Send2_ThrowsException_WhenChannelErrors()
    {
        // Arrange
        var serializeProcessMock = new Mock<ISerializeProcess>();
        serializeProcessMock.Setup(x => x.Serialize(It.IsAny<Base>()))
            .ThrowsAsync(new SpeckleException("Simulated channel error"));

        _serializeProcessFactoryMock.Setup(x => x.CreateSerializeProcess(
            It.IsAny<Uri>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<IProgress<ProgressArgs>>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<SerializeProcessOptions>()
        )).Returns(serializeProcessMock.Object);

        var baseObject = new Base();
        var uri = new Uri("https://example.com");

        // Act & Assert
        // For xUnit, assign the result of the awaited Assert.ThrowsAsync
        var ex = await Assert.ThrowsAsync<SpeckleException>(async () => await _operations.Send2(
            uri,
            "streamId",
            "token",
            baseObject,
            null,
            CancellationToken.None
        ));

        Assert.Equal("Simulated channel error", ex.Message); // Changed from Assert.That
        serializeProcessMock.Verify(x => x.Serialize(baseObject), Times.Once);
        serializeProcessMock.Verify(x => x.DisposeAsync(), Times.Once);
    }
}
