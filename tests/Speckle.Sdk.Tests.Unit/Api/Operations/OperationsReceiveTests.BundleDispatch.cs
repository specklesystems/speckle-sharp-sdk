using Microsoft.Extensions.DependencyInjection;
using Moq;
using Speckle.Sdk.Api;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Pipelines.Receive.Artifacts;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Tests.Unit.Api.Operations;

/// <summary>
/// Speckle 4.0 dispatch: a bundle reference in place of an object hash must route to the artefact rail
/// (Receive2) or fail loud (transport Receive) — never reach the legacy object download.
/// </summary>
public sealed class OperationsReceiveBundleDispatchTests
{
  private const string BUNDLE_REF = "bundle.1.proj.model.ver";
  private static readonly Uri s_url = new("https://example.speckle.invalid");

  private static (IOperations operations, Mock<IArtifactDownloader> downloader) Build()
  {
    var downloader = new Mock<IArtifactDownloader>(MockBehavior.Strict);
    var services = new ServiceCollection();
    services.AddSpeckleSdk(new("Tests", "test"), "v3", typeof(OperationsReceiveBundleDispatchTests).Assembly);
    services.AddSingleton(downloader.Object);
    var provider = services.BuildServiceProvider();
    return (provider.GetRequiredService<IOperations>(), downloader);
  }

  [Fact]
  public async Task Receive2_BundleReference_DispatchesToArtifactDownloader()
  {
    var (operations, downloader) = Build();
    downloader
      .Setup(d =>
        d.DownloadBundleAsync(
          It.Is<Account>(a => a.token == "tok" && a.serverInfo.url == s_url.ToString()),
          "proj",
          "model",
          "ver",
          It.IsAny<string>(),
          It.IsAny<Func<string, bool>?>(),
          It.IsAny<CancellationToken>()
        )
      )
      .ReturnsAsync(Array.Empty<string>());

    // Empty listing = broken promise → loud SpeckleException, never a silent empty tree or a legacy fallback.
    var ex = await Assert.ThrowsAsync<SpeckleException>(() =>
      operations.Receive2(s_url, "proj", BUNDLE_REF, "tok", null, CancellationToken.None)
    );
    Assert.Contains("has no artefact bundle", ex.Message, StringComparison.Ordinal);
    downloader.VerifyAll();
  }

  [Fact]
  public async Task Receive2_BundleReferenceForOtherProject_ThrowsBeforeDownloading()
  {
    var (operations, downloader) = Build();

    var ex = await Assert.ThrowsAsync<SpeckleException>(() =>
      operations.Receive2(s_url, "someOtherProject", BUNDLE_REF, "tok", null, CancellationToken.None)
    );
    Assert.Contains("belongs to project 'proj'", ex.Message, StringComparison.Ordinal);
    downloader.VerifyNoOtherCalls();
  }

  [Fact]
  public async Task Receive2_ObjectHash_NeverTouchesArtifactDownloader()
  {
    var (operations, downloader) = Build();

    // A hash that exists nowhere: the legacy path fails on its own terms, but the downloader is never consulted.
    await Assert.ThrowsAnyAsync<Exception>(() =>
      operations.Receive2(
        s_url,
        "proj",
        "0123456789abcdef0123456789abcdef",
        null,
        null,
        CancellationToken.None,
        new(SkipServer: true)
      )
    );
    downloader.VerifyNoOtherCalls();
  }

  [Fact]
  public async Task Receive_TransportOverload_BundleReference_ThrowsNotSupported()
  {
    var (operations, downloader) = Build();
    MemoryTransport transport = new();

    var ex = await Assert.ThrowsAsync<NotSupportedException>(() =>
      operations.Receive(BUNDLE_REF, transport, transport)
    );
    Assert.Contains("Receive3", ex.Message, StringComparison.Ordinal);
    downloader.VerifyNoOtherCalls();
  }

  [Fact]
  public void Receive_TransportOverload_IsObsolete_PointingAtReceive3()
  {
    var attr = typeof(Speckle.Sdk.Api.Operations)
      .GetMethod(nameof(IOperations.Receive))!
      .GetCustomAttributes(typeof(ObsoleteAttribute), false)
      .Cast<ObsoleteAttribute>()
      .Single();
    Assert.False(attr.IsError);
    Assert.Contains("Receive3", attr.Message, StringComparison.Ordinal);
  }

  [Fact]
  public async Task Receive3_AddressesVersionDirectly_EmptyListingThrows()
  {
    var (operations, downloader) = Build();
    downloader
      .Setup(d =>
        d.DownloadBundleAsync(
          It.Is<Account>(a => a.token == "tok" && a.serverInfo.url == s_url.ToString()),
          "proj",
          "model",
          "ver",
          It.IsAny<string>(),
          It.IsAny<Func<string, bool>?>(),
          It.IsAny<CancellationToken>()
        )
      )
      .ReturnsAsync(Array.Empty<string>());

    var ex = await Assert.ThrowsAsync<SpeckleException>(() =>
      operations.Receive3(s_url, "proj", "model", "ver", "tok", null, CancellationToken.None)
    );
    Assert.Contains("has no artefact bundle", ex.Message, StringComparison.Ordinal);
    downloader.VerifyAll();
  }

  [Fact]
  public void Receive2_IsObsolete_PointingAtReceive3()
  {
    var attr = typeof(Speckle.Sdk.Api.Operations)
      .GetMethod(nameof(IOperations.Receive2))!
      .GetCustomAttributes(typeof(ObsoleteAttribute), false)
      .Cast<ObsoleteAttribute>()
      .Single();
    Assert.False(attr.IsError);
    Assert.Contains("Receive3", attr.Message, StringComparison.Ordinal);
  }
}
