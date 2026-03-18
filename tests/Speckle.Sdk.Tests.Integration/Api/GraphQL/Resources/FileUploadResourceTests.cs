using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Resources;

namespace Speckle.Sdk.Tests.Integration.API.GraphQL.Resources;

public class FileUploadResourceTests : IAsyncLifetime
{
  private FileImportResource Sut => _client.FileImport;
  private IClient _client;
  private Project _project;
  private FileInfo _payload;
  private const string PAYLOAD_CONTENTS = "Hello World!";

  public async Task InitializeAsync()
  {
    var serviceProvider = TestServiceSetup.GetServiceProvider();
    var account = await Fixtures.SeedUser().ConfigureAwait(false);
    _client = serviceProvider.GetRequiredService<IClientFactory>().Create(account);
    _project = await _client.Project.Create(new("test", null, null));

    string filePath = $"{Path.GetTempPath()}/{Guid.NewGuid()}.ifc";
    await using (var writer = File.CreateText(filePath))
    {
      await writer.WriteLineAsync(PAYLOAD_CONTENTS);
    }

    _payload = new FileInfo(filePath);
  }

  public Task DisposeAsync()
  {
    _client.Dispose();
    if (File.Exists(_payload.FullName))
    {
      File.Delete(_payload.FullName);
    }
    return Task.CompletedTask;
  }

  [Fact]
  public async Task GenerateUploadUrl_CreatesUrl()
  {
    var input = new GenerateFileUploadUrlInput(_project.id, "foo.txt");

    var res = await Sut.GenerateUploadUrl(input);
    res.fileId.Should().HaveLength(10);

    //Just check the url path is expected. The query string will contain signatures and dates...
    var expectedUrlPath = new Uri(
      _client.ServerUrl,
      $"http://127.0.0.1:9000/speckle-server/assets/{_project.id}/{res.fileId}"
    );
    new Uri(res.url.GetLeftPart(UriPartial.Path)).Should().Be(expectedUrlPath);
  }

  [Fact]
  public async Task UploadThenDownloadFile()
  {
    //act
    var input = new GenerateFileUploadUrlInput(_project.id, _payload.Name);
    var res = await Sut.GenerateUploadUrl(input);
    _ = await Sut.UploadFile(_payload.FullName, res.url);

    string temp = Path.GetTempFileName();
    await Sut.DownloadFile(_project.id, res.fileId, temp);

    //assert
    File.ReadAllLines(temp).Should().BeEquivalentTo([PAYLOAD_CONTENTS]);
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public async Task StartAndFinishJobFail(bool testSuccessCase)
  {
    //assemble
    Model model = await _client.Model.Create(new("test model", null, _project.id));
    var uploadUrl = await Sut.GenerateUploadUrl(new GenerateFileUploadUrlInput(_project.id, _payload.Name));
    string etag = await Sut.UploadFile(_payload.FullName, uploadUrl.url);
    FileImportResult fakeResult = new(100, 100, 100, "integrationTests", "some value");

    //act
    FileImport job = await Sut.StartFileImportJob(new(_project.id, model.id, uploadUrl.fileId, etag));
    var prePendingJobs = await Sut.GetModelFileImportJobs(_project.id, model.id);

    FileImportInputBase input;
    if (testSuccessCase)
    {
      input = new FileImportSuccessInput()
      {
        projectId = _project.id,
        jobId = job.id,
        result = fakeResult,
        warnings = [],
      };
    }
    else
    {
      input = new FileImportErrorInput()
      {
        projectId = _project.id,
        jobId = job.id,
        reason = "We're testing failure!",
        result = fakeResult,
        warnings = [],
      };
    }

    bool res = await Sut.FinishFileImportJob(input, CancellationToken.None);

    var postPendingJobs = await Sut.GetModelFileImportJobs(_project.id, model.id);

    //assert
    prePendingJobs.items.Should().HaveCount(1);
    prePendingJobs.items.Where(x => x.convertedStatus == 0).Should().HaveCount(1);
    res.Should().BeTrue();
    postPendingJobs.items.Should().HaveCount(1);
    postPendingJobs.items.Where(x => x.convertedStatus == 0).Should().HaveCount(0);
  }
}
