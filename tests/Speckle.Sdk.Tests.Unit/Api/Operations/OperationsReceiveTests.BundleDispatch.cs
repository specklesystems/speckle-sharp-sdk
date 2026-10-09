using Microsoft.Extensions.DependencyInjection;
using Moq;
using Speckle.Sdk.Api;
using Speckle.Sdk.Bundles;
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
  private const string BUNDLE_REF = "bundle.proj.model.ver";
  private static readonly Uri s_url = new("https://example.speckle.invalid");

  private static readonly SpeckleApplication s_app = new()
  {
    HostApplication = "Test",
    HostApplicationVersion = "1.0",
    Slug = "test",
    SpeckleVersion = "0.0.0",
  };

  private static (IOperations operations, Mock<IArtifactDownloader> downloader) Build()
  {
    var (operations, downloader, _) = BuildWithMarker();
    return (operations, downloader);
  }

  private static (
    IOperations operations,
    Mock<IArtifactDownloader> downloader,
    Mock<IVersionReceivedMarker> marker
  ) BuildWithMarker()
  {
    var downloader = new Mock<IArtifactDownloader>(MockBehavior.Strict);
    var marker = new Mock<IVersionReceivedMarker>(MockBehavior.Strict);
    var services = new ServiceCollection();
    services.AddSpeckleSdk(new("Tests", "test"), "v3", typeof(OperationsReceiveBundleDispatchTests).Assembly);
    services.AddSingleton(downloader.Object);
    services.AddSingleton(marker.Object);
    var provider = services.BuildServiceProvider();
    return (provider.GetRequiredService<IOperations>(), downloader, marker);
  }

  /// <summary>A downloader that "downloads" a one-object bundle by writing it into the requested directory.</summary>
  private static void ServeBundle(Mock<IArtifactDownloader> downloader)
  {
    downloader
      .Setup(d =>
        d.DownloadBundleAsync(
          It.IsAny<Account>(),
          "proj",
          "model",
          "ver",
          It.IsAny<string>(),
          It.IsAny<Func<string, bool>?>(),
          It.IsAny<CancellationToken>()
        )
      )
      .Returns(
        (Account _, string _, string _, string _, string dir, Func<string, bool>? _, CancellationToken _) =>
        {
          using var b = new BundleBuilder(s_app, "m", dir);
          b.GetOrAddObject("o").SetProperties(new Dictionary<string, object?> { ["a"] = 1.0 }, "o");
          return Task.FromResult<IReadOnlyList<string>>(b.Build().Files);
        }
      );
  }

  [Fact]
  public async Task Receive3_Success_MarksTheVersionReceived()
  {
    var (operations, downloader, marker) = BuildWithMarker();
    ServeBundle(downloader);
    marker
      .Setup(m => m.MarkAsync(It.Is<Account>(a => a.token == "tok"), "proj", "ver", It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    using var model = await operations.Receive3(
      AccountFor("tok"),
      "proj",
      "model",
      "ver",
      null,
      CancellationToken.None
    );

    Assert.Single(model.Objects);
    marker.VerifyAll();
  }

  [Fact]
  public async Task Receive3_MarkReceivedOff_DoesNotTouchTheServer()
  {
    var (operations, downloader, marker) = BuildWithMarker();
    ServeBundle(downloader);

    using var model = await operations.Receive3(
      AccountFor("tok"),
      "proj",
      "model",
      "ver",
      new ReceiveOptions(MarkReceived: false),
      CancellationToken.None
    );

    Assert.Single(model.Objects);
    marker.VerifyNoOtherCalls();
  }

  [Fact]
  public async Task Receive2_BundlePath_MarksTheVersionReceived()
  {
    var (operations, downloader, marker) = BuildWithMarker();
    ServeBundle(downloader);
    marker
      .Setup(m => m.MarkAsync(It.Is<Account>(a => a.token == "tok"), "proj", "ver", It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    var root = await operations.Receive2(s_url, "proj", BUNDLE_REF, "tok", null, CancellationToken.None);

    Assert.Equal(4, root["version"]);
    marker.VerifyAll();
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
      operations.Receive3(AccountFor("tok"), "proj", "model", "ver", null, CancellationToken.None)
    );
    Assert.Contains("has no artefact bundle", ex.Message, StringComparison.Ordinal);
    downloader.VerifyAll();
  }

  [Theory]
  [InlineData(typeof(Speckle.Sdk.Api.Operations), nameof(IOperations.Receive2))]
  [InlineData(typeof(IOperations), nameof(IOperations.Receive2))] // DI callers see the interface, not the class
  [InlineData(typeof(Speckle.Sdk.Api.Operations), nameof(IOperations.Receive))]
  [InlineData(typeof(IOperations), nameof(IOperations.Receive))]
  public void LegacyReceives_AreObsolete_PointingAtReceive3(Type type, string method)
  {
    var attr = type.GetMethod(method)!
      .GetCustomAttributes(typeof(ObsoleteAttribute), false)
      .Cast<ObsoleteAttribute>()
      .Single();
    Assert.False(attr.IsError);
    Assert.Contains("Receive3", attr.Message, StringComparison.Ordinal);
  }

  [Theory]
  [InlineData(typeof(Speckle.Sdk.Api.Operations))]
  [InlineData(typeof(IOperations))] // DI callers see the interface, not the class
  public void TransportSends_AreObsolete_PointingAtSend3(Type type)
  {
    // ADR-0002: every transport-taking Send overload is frozen legacy surface.
    var sends = type.GetMethods().Where(m => m.Name == nameof(IOperations.Send)).ToList();
    Assert.Equal(3, sends.Count);
    Assert.All(
      sends,
      m =>
      {
        var attr = m.GetCustomAttributes(typeof(ObsoleteAttribute), false).Cast<ObsoleteAttribute>().Single();
        Assert.False(attr.IsError);
        Assert.Contains("Send3", attr.Message, StringComparison.Ordinal);
      }
    );
  }

  [Fact]
  public async Task Receive3_ByUrl_PinnedVersion_NoGraphQL()
  {
    var (operations, downloader) = Build();
    downloader
      .Setup(d =>
        d.DownloadBundleAsync(
          It.IsAny<Account>(),
          "proj",
          "model",
          "ver",
          It.IsAny<string>(),
          It.IsAny<Func<string, bool>?>(),
          It.IsAny<CancellationToken>()
        )
      )
      .ReturnsAsync(Array.Empty<string>());

    await Assert.ThrowsAsync<SpeckleException>(() =>
      operations.Receive3(
        AccountFor("tok"),
        new Uri("https://example.speckle.invalid/projects/proj/models/model@ver"),
        null,
        CancellationToken.None
      )
    );
    downloader.VerifyAll();
  }

  [Fact]
  public async Task Receive3_ByUrl_NotAModelUrl_Throws()
  {
    var (operations, downloader) = Build();
    await Assert.ThrowsAsync<ArgumentException>(() =>
      operations.Receive3(
        AccountFor("tok"),
        new Uri("https://example.speckle.invalid/streams/abc"),
        null,
        CancellationToken.None
      )
    );
    downloader.VerifyNoOtherCalls();
  }

  [Fact]
  public async Task Receive3_ByUrl_OtherServerThanAccount_Throws()
  {
    var (operations, downloader) = Build();
    var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
      operations.Receive3(
        AccountFor("tok"),
        new Uri("https://other.speckle.invalid/projects/proj/models/model@ver"),
        null,
        CancellationToken.None
      )
    );
    Assert.Equal("modelUrl", ex.ParamName);
    downloader.VerifyNoOtherCalls();
  }

  private static Account AccountFor(string token) =>
    new()
    {
      token = token,
      serverInfo = new() { url = s_url.ToString() },
      userInfo = new(),
    };
}
