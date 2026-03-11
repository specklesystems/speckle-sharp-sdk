using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Common;
using Speckle.Sdk.Pipelines.Progress;

namespace Speckle.Sdk.Tests.Integration.Pipelines.Progress;

[Trait("Server", "Internal")]
public class IngestionProgressManagerTests : IAsyncLifetime
{
  private IIngestionProgressManagerFactory _factory;
  private IClient _client;
  private Project _project;
  private Model _model;
  private ModelIngestion _ingestion;

  public async Task InitializeAsync()
  {
    var serviceProvider = TestServiceSetup.GetServiceProvider();
    _factory = serviceProvider.GetRequiredService<IIngestionProgressManagerFactory>();

    _client = await Fixtures.SeedUserWithClient();
    _project = await _client.Project.Create(new("test", null, default));
    _model = await _client.Model.Create(new("test", null, _project.id));
    _ingestion = await _client.Ingestion.Create(
      new(_model.id, _project.id, "Testing ingestion", new("integrationTests", "0.0.0", null, null))
    );
  }

  [Fact]
  public async Task TestProgress_NoThrottle()
  {
    var sut = _factory.CreateInstance(_client, _ingestion, TimeSpan.Zero, CancellationToken.None);
    const string FIRST_MESSAGE = "This is a test 123";
    const string SECOND_MESSAGE = "This is another test 321";

    // first message (should go through)
    sut.Report(new CardProgress(FIRST_MESSAGE, 0.123123123d));
    await sut.LastUpdate.NotNull();
    var res = await _client.Ingestion.Get(_ingestion.id, _project.id, CancellationToken.None);

    Assert.Equal(FIRST_MESSAGE, res.statusData.progressMessage);

    // second message (should also go through)
    sut.Report(new CardProgress(SECOND_MESSAGE, 0.321321321d));
    await sut.LastUpdate.NotNull();
    res = await _client.Ingestion.Get(_ingestion.id, _project.id, CancellationToken.None);

    Assert.Equal(SECOND_MESSAGE, res.statusData.progressMessage);
  }

  [Fact]
  public async Task TestProgress_WithThrottle()
  {
    var sut = _factory.CreateInstance(_client, _ingestion, TimeSpan.FromMinutes(5), CancellationToken.None);
    const string EXPECTED_MESSAGE = "First message should go through 123";

    // first message (should go through)
    sut.Report(new CardProgress(EXPECTED_MESSAGE, 0.123123123d));
    await sut.LastUpdate.NotNull();
    var res = await _client.Ingestion.Get(_ingestion.id, _project.id, CancellationToken.None);

    Assert.Equal(EXPECTED_MESSAGE, res.statusData.progressMessage);

    // second message (should be dropped)
    sut.Report(new CardProgress("Second message, should be dropped", 0.321321321d));
    await sut.LastUpdate.NotNull();
    res = await _client.Ingestion.Get(_ingestion.id, _project.id, CancellationToken.None);

    Assert.Equal(EXPECTED_MESSAGE, res.statusData.progressMessage);
  }

  public Task DisposeAsync()
  {
    _client.Dispose();
    return Task.CompletedTask;
  }
}
