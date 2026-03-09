using Moq;
using Speckle.Sdk.Pipelines.Progress;

namespace Speckle.Sdk.Tests.Unit.Pipelines.Progress;

public class AggregateProgressTests
{
  [Fact]
  public void Report_InvokesReportOnAllInnerProgresses()
  {
    var mock1 = new Mock<IProgress<int>>();
    var mock2 = new Mock<IProgress<int>>();
    const int TEST_VALUE = 42;
    var target = new AggregateProgress<int>(mock1.Object, mock2.Object);

    target.Report(TEST_VALUE);

    mock1.Verify(x => x.Report(TEST_VALUE), Times.Once);
    mock2.Verify(x => x.Report(TEST_VALUE), Times.Once);
  }
}
