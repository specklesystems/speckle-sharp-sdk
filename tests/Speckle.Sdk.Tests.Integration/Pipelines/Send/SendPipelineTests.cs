using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Enums;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Models;
using Speckle.Sdk.Pipelines.Progress;
using Speckle.Sdk.Pipelines.Send;

namespace Speckle.Sdk.Tests.Integration.Pipelines.Send;

[Trait("Server", "Internal")]
public sealed class SendPipelineTests : IAsyncLifetime
{
  private Project _project;
  private Model _model;
  private IClient _client;

  private ISendPipelineFactory _factory;

  public async Task InitializeAsync()
  {
    var serviceProvider = TestServiceSetup.GetServiceProvider();
    _factory = serviceProvider.GetRequiredService<ISendPipelineFactory>();

    _client = await Fixtures.SeedUserWithClient();
    _project = await _client.Project.Create(new("Blobber", "Flobber", ProjectVisibility.Private));
    _model = await _client.Model.Create(new("Llobber", "Clobber", _project.id));
  }

  private static string ReadNdJsonGz(FileInfo file)
  {
    using FileStream fileStream = file.OpenRead();
    using GZipStream gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
    using StreamReader reader = new StreamReader(gzipStream, Encoding.UTF8);
    return reader.ReadToEnd();
  }

  [Fact]
  public async Task SendNdjson()
  {
    Base myObject = Fixtures.GenerateNestedObject();

    var ingestion = await _client.Ingestion.Create(
      new(_model.id, _project.id, "Starting send test", new("IntegrationTests", "0", null, null))
    );

    using SendPipeline sender = _factory.CreateInstance(
      ingestion,
      _client.Account,
      new NullProgress<StreamProgressArgs>(),
      CancellationToken.None
    );
    await sender.Process(myObject);
    var file = await sender.DiskStore.CompleteAsync();

    string ndjson = ReadNdJsonGz(file.FileInfo);

    await Verify(ndjson);
  }

  [Fact]
  public async Task SerializeConsistency()
  {
    Base myObject = Fixtures.GenerateNestedObject();

    var ingestion = await _client.Ingestion.Create(
      new(_model.id, _project.id, "Starting send test", new("IntegrationTests", "0", null, null))
    );

    //SEND
    ObjectReference firstSend;
    using (
      SendPipeline sender = _factory.CreateInstance(
        ingestion,
        _client.Account,
        new NullProgress<StreamProgressArgs>(),
        CancellationToken.None
      )
    )
    {
      firstSend = await sender.Process(myObject);
      await sender.WaitForUpload();
    }

    var secondIngestion = await _client.Ingestion.Create(
      new(_model.id, _project.id, "Starting again", new("IntegrationTests", "0", null, null))
    );

    //SEND AGAIN!
    ObjectReference secondSend;
    using (
      SendPipeline sender = _factory.CreateInstance(
        secondIngestion,
        _client.Account,
        new NullProgress<StreamProgressArgs>(),
        CancellationToken.None
      )
    )
    {
      secondSend = await sender.Process(myObject);
      await sender.WaitForUpload();
    }

    Assert.Equal(firstSend.referencedId, secondSend.referencedId);
  }

  [Fact]
  public async Task SerializeInvalidDataThrows()
  {
    Base myObject = Fixtures.GenerateNestedObject();
    myObject["invalidProp"] = new StringBuilder(); //Serializer does not support serializing this type

    var ingestion = await _client.Ingestion.Create(
      new(_model.id, _project.id, "Starting send test", new("IntegrationTests", "0", null, null))
    );

    await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
    {
      using SendPipeline sender = _factory.CreateInstance(
        ingestion,
        _client.Account,
        new NullProgress<StreamProgressArgs>(),
        CancellationToken.None
      );
      await sender.Process(myObject);
      await sender.WaitForUpload();
    });
  }

  [Fact]
  public async Task SendNoIngestionThrows()
  {
    Base myObject = Fixtures.GenerateNestedObject();

    await Assert.ThrowsAsync<HttpRequestException>(async () =>
    {
      using var sender = _factory.CreateInstance(
        _project.id,
        "not-a-real-ingestion",
        _client.Account,
        new NullProgress<StreamProgressArgs>(),
        CancellationToken.None
      );
      await sender.Process(myObject);
      await sender.WaitForUpload();
    });
  }

  public Task DisposeAsync()
  {
    _client?.Dispose();
    return Task.CompletedTask;
  }
}
