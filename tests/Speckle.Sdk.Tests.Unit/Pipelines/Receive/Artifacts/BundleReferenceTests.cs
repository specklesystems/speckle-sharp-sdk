using Speckle.Sdk.Pipelines.Receive.Artifacts;

namespace Speckle.Sdk.Tests.Unit.Pipelines.Receive.Artifacts;

public class BundleReferenceTests
{
  [Fact]
  public void TryParse_ValidReference_ParsesTriple()
  {
    Assert.True(BundleReference.TryParse("bundle.1.proj123.model456.ver789", out var reference));
    Assert.Equal(1, reference!.ContractVersion);
    Assert.Equal("proj123", reference.ProjectId);
    Assert.Equal("model456", reference.ModelId);
    Assert.Equal("ver789", reference.VersionId);
    Assert.Equal("bundle.1.proj123.model456.ver789", reference.ToString());
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("0123456789abcdef0123456789abcdef")] // a content hash
  [InlineData("Bundle.1.p.m.v")] // prefix is case-sensitive by contract
  [InlineData("bundles.1.p.m.v")]
  public void TryParse_NotABundleReference_ReturnsFalse(string? id)
  {
    Assert.False(BundleReference.TryParse(id, out var reference));
    Assert.Null(reference);
    Assert.False(BundleReference.IsBundleReference(id));
  }

  [Theory]
  [InlineData("bundle.")]
  [InlineData("bundle.1")]
  [InlineData("bundle.1.p.m")]
  [InlineData("bundle.1.p.m.v.extra")]
  [InlineData("bundle.1..m.v")]
  [InlineData("bundle.x.p.m.v")]
  public void TryParse_MalformedReference_Throws(string id)
  {
    // Looks like ours but isn't usable: must surface, never fall through to the legacy path.
    Assert.True(BundleReference.IsBundleReference(id));
    Assert.Throws<SpeckleException>(() => BundleReference.TryParse(id, out _));
  }

  [Fact]
  public void TryParse_NewerContractVersion_ThrowsWithUpgradeGuidance()
  {
    var ex = Assert.Throws<SpeckleException>(() => BundleReference.TryParse("bundle.2.p.m.v", out _));
    Assert.Contains("Upgrade Speckle.Sdk", ex.Message, StringComparison.Ordinal);
  }
}
