using Microsoft.Extensions.DependencyInjection;
using Moq;
using Speckle.Sdk.Api;
using Speckle.Sdk.Bundles;

namespace Speckle.Sdk.Tests.Unit.Api.Operations;

/// <summary>What Send3 decides before touching the network. The sign/PUT/complete flow itself is covered by the
/// server-backed integration test (<c>SendReceiveBundleTests</c>).</summary>
public sealed class OperationsSend3Tests : IDisposable
{
  private static readonly SpeckleApplication s_app = new()
  {
    HostApplication = "Test",
    HostApplicationVersion = "1.0",
    Slug = "test",
    SpeckleVersion = "0.0.0",
  };

  private readonly string _dir = Path.Combine(Path.GetTempPath(), "Send3Tests", Guid.NewGuid().ToString("N"));

  private static (IOperations operations, Mock<IBundleSender> uploads) Build()
  {
    var uploads = new Mock<IBundleSender>(MockBehavior.Strict);
    var services = new ServiceCollection();
    services.AddSpeckleSdk(new("Tests", "test"), "v3", typeof(OperationsSend3Tests).Assembly);
    services.AddSingleton(uploads.Object);
    return (services.BuildServiceProvider().GetRequiredService<IOperations>(), uploads);
  }

  [Fact]
  public async Task Send3_ByUrl_PinnedVersion_RejectedBeforeAnyIO()
  {
    var (operations, uploads) = Build();
    using var builder = new BundleBuilder(s_app, "m", _dir);

    var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
      operations.Send3(
        "https://example.speckle.invalid/projects/p/models/m@v",
        builder,
        "tok",
        null,
        CancellationToken.None
      )
    );
    Assert.Contains("@versionId", ex.Message, StringComparison.Ordinal);
    uploads.VerifyNoOtherCalls();
  }

  [Fact]
  public async Task Send3_ByUrl_NotAModelUrl_RejectedBeforeAnyIO()
  {
    var (operations, uploads) = Build();
    using var builder = new BundleBuilder(s_app, "m", _dir);

    await Assert.ThrowsAsync<ArgumentException>(() =>
      operations.Send3("https://example.speckle.invalid/streams/abc", builder, "tok", null, CancellationToken.None)
    );
    uploads.VerifyNoOtherCalls();
  }

  [Fact]
  public void SendResult_BundleReference_IsTheDispatchForm()
  {
    var r = new SendResult("proj", "model", "ver", "ing", 3);
    Assert.Equal("bundle.proj.model.ver", r.BundleReference);
  }

  public void Dispose()
  {
    if (Directory.Exists(_dir))
    {
      Directory.Delete(_dir, true);
    }
  }
}
