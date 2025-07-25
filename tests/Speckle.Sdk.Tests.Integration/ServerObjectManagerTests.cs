using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.V2;

namespace Speckle.Sdk.Tests.Integration;

public class ServerObjectManagerTests : IAsyncLifetime
{
  private IServerObjectManager _sut;
  private IClient _client;
  private Project _project;

  public async Task InitializeAsync()
  {
    var serviceProvider = TestServiceSetup.GetServiceProvider();
    var account = await Fixtures.SeedUser().ConfigureAwait(false);
    _client = serviceProvider.GetRequiredService<IClientFactory>().Create(account);
    var factory = serviceProvider.GetRequiredService<IServerObjectManagerFactory>();
    _project = await _client.Project.Create(new("test", null, null));
    _sut = factory.Create(_client.ServerUrl, _project.id, account.token);
  }

  [Fact]
  public async Task BlobEndToEndTest()
  {
    //assembly
    const string PAYLOAD = "Hello World!";
    string filePath = Path.GetTempFileName();
    await using (var writer = File.CreateText(filePath))
    {
      await writer.WriteLineAsync(PAYLOAD);
    }
    string id = HashUtility.HashFile(filePath);
    string prefixedId = $"blob:{id}";

    //act
    var preDiff = await _sut.HasBlobs([id], CancellationToken.None);
    await _sut.UploadBlobs([(prefixedId, filePath)], null, CancellationToken.None);
    var postDiff = await _sut.HasBlobs([id], CancellationToken.None);
    var res = await _sut.DownloadBlob(id, null, CancellationToken.None);

    //assert
    preDiff.Should().BeEquivalentTo([id]);
    postDiff.Should().BeEquivalentTo([]);
    var file = new FileInfo(res);
    file.Name.Should().StartWith(id[..Blob.LocalHashPrefixLength]);
    file.Directory?.FullName.Should().Be(SpecklePathProvider.BlobStoragePath());

    string[] lines = await File.ReadAllLinesAsync(res);
    lines[0].Should().Be(PAYLOAD);
    lines.Length.Should().Be(1);
  }

  [Fact]
  public async Task DownloadBlob_Throws_NonExistentId()
  {
    var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
      await _sut.DownloadBlob("non-existent-id", null, CancellationToken.None)
    );
    await Verify(ex);
  }

  [Fact]
  public async Task DownloadBlob_Throws_Cancellation()
  {
    using var cancellationTokenSource = new CancellationTokenSource();
    cancellationTokenSource.Cancel();

    await Assert.ThrowsAsync<OperationCanceledException>(async () =>
      await _sut.DownloadBlob("non-existent-id", null, cancellationTokenSource.Token)
    );
  }

  [Fact]
  public async Task UploadBlobs_Throws_Cancellation()
  {
    using var cancellationTokenSource = new CancellationTokenSource();
    cancellationTokenSource.Cancel();

    await Assert.ThrowsAsync<OperationCanceledException>(async () =>
      await _sut.UploadBlobs([("id", "path")], null, cancellationTokenSource.Token)
    );
  }

  [Fact]
  public async Task HasBlobs_Throws_Cancellation()
  {
    using var cancellationTokenSource = new CancellationTokenSource();
    cancellationTokenSource.Cancel();

    await Assert.ThrowsAsync<OperationCanceledException>(async () =>
      await _sut.HasBlobs(["non-existent-id"], cancellationTokenSource.Token)
    );
  }

  public Task DisposeAsync()
  {
    _client.Dispose();
    return Task.CompletedTask;
  }
}
