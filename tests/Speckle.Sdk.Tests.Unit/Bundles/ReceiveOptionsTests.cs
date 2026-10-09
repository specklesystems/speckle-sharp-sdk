using Speckle.Sdk.Bundles;

namespace Speckle.Sdk.Tests.Unit.Bundles;

public class ReceiveOptionsTests
{
  [Theory]
  [InlineData("08de6a66ec.viewer.dat", false)]
  [InlineData("08de6a66ec.viewer.idx", false)]
  [InlineData("08de6a66ec.VIEWER.DAT", false)]
  [InlineData("08de6a66ec.geometries.parquet", true)]
  [InlineData("08de6a66ec.eav.parquet", true)]
  [InlineData("08de6a66ec.envelope.nodes.parquet", true)]
  public void Default_SkipsOnlyViewerArtifacts(string fileName, bool expected)
  {
    Assert.Equal(expected, ReceiveOptions.Default.ShouldDownload(fileName));
  }

  [Fact]
  public void IncludeViewerArtifacts_DownloadsEverything()
  {
    var options = new ReceiveOptions(IncludeViewerArtifacts: true);
    Assert.True(options.ShouldDownload("08de6a66ec.viewer.dat"));
    Assert.True(options.ShouldDownload("08de6a66ec.eav.parquet"));
  }

  [Theory]
  [InlineData("08de6a66ec.geometries.parquet")]
  [InlineData("08de6a66ec.geometries.000.parquet")]
  public void IncludeGeometryFalse_SkipsShards(string fileName)
  {
    var options = new ReceiveOptions(IncludeGeometry: false);
    Assert.False(options.ShouldDownload(fileName));
    Assert.True(options.ShouldDownload("08de6a66ec.eav.parquet"));
    Assert.True(ReceiveOptions.Default.ShouldDownload(fileName));
  }
}
