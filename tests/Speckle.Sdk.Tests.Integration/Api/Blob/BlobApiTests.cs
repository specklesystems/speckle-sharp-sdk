using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.Blob;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Tests.Integration.Api.Blob;

public class BlobApiTests : IAsyncLifetime
{
  private IBlobApi _blobApi;
  private IClient _client;
  private Project _project;

  public async Task InitializeAsync()
  {
    var serviceProvider = TestServiceSetup.GetServiceProvider();
    var account = await Fixtures.SeedUser().ConfigureAwait(false);
    _client = serviceProvider.GetRequiredService<IClientFactory>().Create(account);
    var factory = serviceProvider.GetRequiredService<IBlobApiFactory>();
    _project = await _client.Project.Create(new("test", null, null));
    _blobApi = factory.Create(account);
  }

  [Fact(Skip = "Blob creation returns 201, but fetching the blob returns 404. Seems like a server regression")]
  public async Task BlobEndToEndTest()
  {
    //assemble
    const string PAYLOAD = "Hello World!";
    string filePath = Path.GetTempFileName();
    await using (var writer = File.CreateText(filePath))
    {
      await writer.WriteLineAsync(PAYLOAD);
    }
    string id = HashUtility.HashFile(filePath);

    //act
    var preDiff = await _blobApi.HasBlobs(_project.id, [id], CancellationToken.None);
    await _blobApi.UploadBlobs(_project.id, [(id, filePath)], null, CancellationToken.None);
    var postDiff = await _blobApi.HasBlobs(_project.id, [id], CancellationToken.None);
    var res = await _blobApi.DownloadBlob(_project.id, id);

    //assert
    preDiff.Should().BeEquivalentTo([id]);
    postDiff.Should().BeEquivalentTo([]);
    var file = new FileInfo(res);
    file.Name.Should().StartWith(id[..Models.Blob.LocalHashPrefixLength]);
    file.Directory?.FullName.Should().Be(SpecklePathProvider.BlobStoragePath());

    string[] lines = await File.ReadAllLinesAsync(res);
    lines[0].Should().Be(PAYLOAD);
    lines.Length.Should().Be(1);
  }

  public Task DisposeAsync()
  {
    _client.Dispose();
    return Task.CompletedTask;
  }
}
