using Speckle.Sdk.Pipelines.Progress;

namespace Speckle.Sdk.Tests.Unit.Pipelines.Progress;

public class RenderedStreamProgressTests
{
  [Theory]
  [InlineData(1, "B", 1.0)]
  [InlineData(1024, "B", 1.0)]
  [InlineData(1024 + 1, "KB", 1.0 / 1024)]
  [InlineData(1024 * 1024, "KB", 1.0 / 1024)]
  [InlineData(1024 * 1024 + 1, "MB", 1.0 / (1024 * 1024))]
  [InlineData(1024 * 1024 * 1024, "MB", 1.0 / (1024 * 1024))]
  [InlineData(1024 * 1024 * 1024 + 1, "GB", 1.0 / (1024 * 1024 * 1024))]
  [InlineData(1024L * 1024L * 1024L * 1024L, "GB", 1.0 / (1024L * 1024L * 1024L))]
  public void GetFileSizeRendering_WithPositiveValue_ReturnsCorrectSuffix(
    long value,
    string expectedSuffix,
    double expectedScaleFactor
  )
  {
    var result = RenderedStreamProgress.GetFileSizeRendering(value);

    Assert.Equal(expectedSuffix, result.suffix);
    Assert.Equal(expectedScaleFactor, result.scaleFactor);
  }

  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  [InlineData(-1000)]
  public void GetFileSizeRendering_WithNonPositiveValue_ReturnsBytesSuffix(long value)
  {
    var result = RenderedStreamProgress.GetFileSizeRendering(value);

    Assert.Equal("B", result.suffix);
    Assert.Equal(1d, result.scaleFactor);
  }

  [Theory]
  [InlineData(long.MaxValue)]
  public void GetFileSizeRendering_WithVeryLargeValue_ThrowsArgumentOutOfRangeException(long value)
  {
    Assert.Throws<ArgumentOutOfRangeException>(() => RenderedStreamProgress.GetFileSizeRendering(value));
  }
}
