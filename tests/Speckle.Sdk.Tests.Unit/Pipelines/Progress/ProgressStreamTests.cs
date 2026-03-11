using System.Diagnostics.CodeAnalysis;
using Moq;
using Speckle.Sdk.Pipelines.Progress;

namespace Speckle.Sdk.Tests.Unit.Pipelines.Progress;

[SuppressMessage(
  "Performance",
  "CA1835:Prefer the \'Memory\'-based overloads for \'ReadAsync\' and \'WriteAsync\'",
  Justification = "Need to test it"
)]
public class ProgressStreamTests : IDisposable
{
  private readonly Mock<Stream> _innerStreamMock;
  private readonly Mock<IProgress<StreamProgressArgs>> _progressMock;
  private readonly ProgressStream _sut;

  public ProgressStreamTests()
  {
    // Setup the mocks
    _innerStreamMock = new Mock<Stream>();
    _innerStreamMock.Setup(s => s.Length).Returns(1024L);

    _progressMock = new Mock<IProgress<StreamProgressArgs>>();

    // Inject mocks into the System Under Test
    _sut = new ProgressStream(_innerStreamMock.Object, _progressMock.Object);
  }

  [Fact]
  public async Task ReadAsync_Should_CallInnerStreamAndReportProgress()
  {
    // Arrange
    var buffer = new byte[10];
    _innerStreamMock
      .Setup(s => s.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None))
      .Returns(Task.FromResult(5));

    // Act
    await _sut.ReadAsync(buffer, 0, buffer.Length);

    // Assert - Inner Stream Read was called
    _innerStreamMock.Verify(s => s.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None), Times.Once);

    // Assert - Progress Report was called with the correct byte count
    _progressMock.Verify(p => p.Report(It.IsAny<StreamProgressArgs>()), Times.Once);
  }

  [Fact]
  public async Task WriteAsync_Should_CallInnerStreamAndReportProgress()
  {
    // Arrange
    var buffer = new byte[10];
    _innerStreamMock
      .Setup(s => s.WriteAsync(buffer, 0, buffer.Length, CancellationToken.None))
      .Returns(Task.FromResult(5));

    // Act
    await _sut.WriteAsync(buffer, 0, buffer.Length);

    // Assert - Inner Stream Write was called
    _innerStreamMock.Verify(s => s.WriteAsync(buffer, 0, buffer.Length, CancellationToken.None), Times.Once);

    // Assert - Progress Report was called with the correct byte count
    _progressMock.Verify(p => p.Report(It.IsAny<StreamProgressArgs>()), Times.Once);
  }

  public void Dispose()
  {
    _sut.Dispose();
  }
}
