using Speckle.Sdk.Pipelines.Receive.Artifacts;

namespace Speckle.Sdk.Tests.Unit.Pipelines.Receive.Artifacts;

public sealed class ArtifactDownloaderTests
{
  [Theory]
  [InlineData("08de6a66ec.eav.objects.parquet")]
  [InlineData("viewer.dat")]
  public void IsBareFileName_AcceptsPlainNames(string name) => Assert.True(ArtifactDownloader.IsBareFileName(name));

  [Theory]
  [InlineData("")]
  [InlineData(null)]
  [InlineData(".")]
  [InlineData("..")]
  [InlineData("../x.parquet")]
  [InlineData("a/b.parquet")]
  [InlineData("/etc/passwd")]
  [InlineData("C:\\x.parquet")]
  [InlineData("..\\x.parquet")]
  public void IsBareFileName_RejectsAnythingThatCouldLeaveTheReceiveDirectory(string? name) =>
    Assert.False(ArtifactDownloader.IsBareFileName(name));
}
