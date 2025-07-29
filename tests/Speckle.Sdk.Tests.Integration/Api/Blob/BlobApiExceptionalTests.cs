using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.Blob;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Tests.Integration.Api.GraphQL.Resources;

public class BlobApiExceptionalTests : IAsyncLifetime
{
  private IBlobApi _sut;
  private IClient _client;
  private Project _project;

  public async Task InitializeAsync()
  {
    var serviceProvider = TestServiceSetup.GetServiceProvider();
    var account = await Fixtures.SeedUser().ConfigureAwait(false);
    _client = serviceProvider.GetRequiredService<IClientFactory>().Create(account);
    var factory = serviceProvider.GetRequiredService<IBlobApiFactory>();
    _project = await _client.Project.Create(new("test", null, null));
    _sut = factory.Create(account);
  }

  [Fact]
  public async Task DownloadBlob_Throws_NonExistentId()
  {
    var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
      await _sut.DownloadBlob(_project.id, "non-existent-id", cancellationToken: CancellationToken.None)
    );
    await Verify(ex);
  }

  [Fact]
  public async Task DownloadBlob_Throws_NonExistentProject()
  {
    var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
      await _sut.DownloadBlob("non-existent-project", "non-existent-id", cancellationToken: CancellationToken.None)
    );
    await Verify(ex);
  }

  [Fact]
  public async Task DownloadBlob_Throws_Cancellation()
  {
    using var cancellationTokenSource = new CancellationTokenSource();
    cancellationTokenSource.Cancel();

    await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
      await _sut.DownloadBlob(_project.id, "non-existent-id", cancellationToken: cancellationTokenSource.Token)
    );
  }

  [Fact]
  public async Task UploadBlobs_Throws_NonExistentProject()
  {
    const string PAYLOAD = "Hello World!";
    string filePath = Path.GetTempFileName();
    await using (var writer = File.CreateText(filePath))
    {
      await writer.WriteLineAsync(PAYLOAD);
    }
    string id = HashUtility.HashFile(filePath);
    var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
      await _sut.UploadBlobs("non-existent-project", [(id, filePath)], null, CancellationToken.None)
    );
    await Verify(ex);
  }

  [Fact]
  public async Task UploadBlobs_Throws_Cancellation()
  {
    using var cancellationTokenSource = new CancellationTokenSource();
    cancellationTokenSource.Cancel();

    await Assert.ThrowsAsync<OperationCanceledException>(async () =>
      await _sut.UploadBlobs(_project.id, [("id", "path")], null, cancellationTokenSource.Token)
    );
  }

  [Fact]
  public async Task HasBlobs_Throws_Cancellation()
  {
    using var cancellationTokenSource = new CancellationTokenSource();
    cancellationTokenSource.Cancel();

    await Assert.ThrowsAsync<OperationCanceledException>(async () =>
      await _sut.HasBlobs(_project.id, ["non-existent-id"], cancellationTokenSource.Token)
    );
  }

  [Fact]
  public async Task HasBlobs_Throws_NonExistentProject()
  {
    var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
      await _sut.HasBlobs("non-existent-project", ["non-existent-id"], CancellationToken.None)
    );
    await Verify(ex);
  }

  public Task DisposeAsync()
  {
    _client.Dispose();
    return Task.CompletedTask;
  }
}
