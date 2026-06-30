using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Enums;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Resources;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports;
using Version = Speckle.Sdk.Api.GraphQL.Models.Version;

namespace Speckle.Sdk.Tests.Integration.API.GraphQL.Resources;

[Trait("Server", "Internal")]
public sealed class ModelIngestionResourceTests : IAsyncLifetime
{
  private IClient _testUser;
  private ModelIngestionResource Sut => _testUser.Ingestion;
  private Project _project;
  private Model _model;
  private IOperations _operations;

  public ValueTask DisposeAsync() => ValueTask.CompletedTask;

  public async ValueTask InitializeAsync()
  {
    var serviceProvider = TestServiceSetup.GetServiceProvider();
    _operations = serviceProvider.GetRequiredService<IOperations>();

    _testUser = await Fixtures.SeedUserWithClient();
    _project = await _testUser.Project.Create(new("Test project", "", null));
    _model = await _testUser.Model.Create(new("Test Model 1", "", _project.id));
  }

  [Fact]
  public async Task CreateAndError()
  {
    var createInput = new ModelIngestionCreateInput(
      _model.id,
      _project.id,
      "Starting processing",
      new(".NET test runner", "0.0.0", null, null)
    );
    ModelIngestion ingest = await Sut.Create(createInput, TestContext.Current.CancellationToken);

    var errorInput = new ModelIngestionFailedInput(ingest.id, _project.id, "A bad thing happened", "Over hear!");
    var res = await Sut.FailWithError(errorInput, TestContext.Current.CancellationToken);
    Assert.Equal(ingest.id, res.id);
  }

  [Fact]
  public async Task CreateAndUpdate()
  {
    var createInput = new ModelIngestionCreateInput(
      _model.id,
      _project.id,
      "Starting processing",
      new(".NET test runner", "0.0.0", null, null)
    );
    ModelIngestion ingest = await Sut.Create(createInput, TestContext.Current.CancellationToken);

    await Update(null, "None");
    await Update(0.1, "0.1");
    await Update(0.5, "Whoa-oh! We're half way there!");
    await Update(1, "Finished");
    await Update(0.2, "Back to processing again");

    async Task Update(double? progress, string message)
    {
      var updateInput = new ModelIngestionUpdateInput(ingest.id, _project.id, message, progress);
      var res = await Sut.UpdateProgress(updateInput, TestContext.Current.CancellationToken);

      Assert.Equal(message, res.statusData.progressMessage);
      Assert.False(res.cancellationRequested);
      Assert.Equal(ModelIngestionStatus.processing, res.statusData.status);
    }
  }

  [Fact]
  public async Task CreateAndCancel()
  {
    var createInput = new ModelIngestionCreateInput(
      _model.id,
      _project.id,
      "Starting processing",
      new(".NET test runner", "0.0.0", null, null)
    );
    ModelIngestion ingest = await Sut.Create(createInput, TestContext.Current.CancellationToken);

    var input = new ModelIngestionCancelledInput(
      ingest.id,
      _project.id,
      cancellationMessage: "This was cancelled for testing purposes"
    );
    var res = await Sut.FailWithCancel(input, TestContext.Current.CancellationToken);
    Assert.Equal(ingest.id, res.id);
  }

  [Fact]
  public async Task CreateAndFailWithInvalid()
  {
    var createInput = new ModelIngestionCreateInput(
      _model.id,
      _project.id,
      "Starting processing",
      new(".NET test runner", "0.0.0", null, null)
    );
    ModelIngestion ingest = await Sut.Create(createInput, TestContext.Current.CancellationToken);

    var input = new ModelIngestionInvalidInput(
      ingest.id,
      _project.id,
      validationMessage: "The users input was invalid"
    );
    var res = await Sut.FailWithInvalid(input, TestContext.Current.CancellationToken);
    Assert.Equal(ingest.id, res.id);
  }

  [Fact]
  public async Task CreateAndComplete()
  {
    ModelIngestionCreateInput createInput = new(
      _model.id,
      _project.id,
      "Starting processing",
      new(".NET test runner", "0.0.0", null, null)
    );
    ModelIngestion ingest = await Sut.Create(createInput, TestContext.Current.CancellationToken);

    Base myObject = Fixtures.GenerateNestedObject();
    var sendResult = await _operations.Send2(
      _testUser.ServerUrl,
      _project.id,
      _testUser.Account.token,
      myObject,
      new Progress<ProgressArgs>(x =>
      {
        var updateInput = new ModelIngestionUpdateInput(
          ingest.id,
          _project.id,
          $"{x.Count} / {x.Total}",
          x.Total == null ? null : x.Count / x.Total
        );
        _ = Sut.UpdateProgress(updateInput, TestContext.Current.CancellationToken).Result;
      }),
      CancellationToken.None,
      new(true, true)
    );

    ModelIngestionSuccessInput finish = new(ingest.id, _project.id, sendResult.RootId, "yay!");
    string versionId = await Sut.Complete(finish, TestContext.Current.CancellationToken);
    Version version = await _testUser.Version.Get(versionId, _project.id, TestContext.Current.CancellationToken);
    ModelIngestion finalIngestion = await _testUser.Ingestion.Get(
      ingest.id,
      _project.id,
      TestContext.Current.CancellationToken
    );
    Assert.Equal(version.id, versionId);
    Assert.Equal(sendResult.RootId, version.referencedObject);
    Assert.Equal(finalIngestion.statusData.versionId, versionId);
  }

  [Fact]
  public async Task CreateAndGet()
  {
    var createInput = new ModelIngestionCreateInput(
      _model.id,
      _project.id,
      "Starting processing",
      new(".NET test runner", "0.0.0", null, null)
    );
    ModelIngestion ingest = await Sut.Create(createInput, TestContext.Current.CancellationToken);

    ModelIngestion res = await Sut.Get(ingest.id, _project.id, TestContext.Current.CancellationToken);
    Assert.Equal(ingest.id, res.id);
    Assert.Equal(ingest.statusData.status, res.statusData.status);
    Assert.Equal(ingest.statusData.versionId, res.statusData.versionId);
    Assert.Null(res.statusData.versionId);
    Assert.Equal(_model.id, res.modelId);
    Assert.Equal(_project.id, res.projectId);
    Assert.Equal(_testUser.Account.userInfo.id, res.userId);
  }

  [Fact]
  public async Task TestRequeue()
  {
    //Not sure if is desirable that ingestions created by the modelIngestionMutations.create mutation can be re-queued
    //But the server allows it, so we test it
    var createInput = new ModelIngestionCreateInput(
      _model.id,
      _project.id,
      "Starting processing",
      new(".NET test runner", "0.0.0", null, null)
    );
    var ingestion = await Sut.Create(createInput, TestContext.Current.CancellationToken);
    var res = await Sut.Requeue(
      new(ingestion.id, _project.id, "we'll try and requeue this ingestion"),
      TestContext.Current.CancellationToken
    );

    Assert.Equal(ingestion.id, res.id);
    Assert.Equal(ModelIngestionStatus.queued, res.statusData.status);
  }

  [Fact]
  public async Task TestStartProcessing()
  {
    //Not sure if is desirable that StartProcessing can be used by ingestions created by the modelIngestionMutations.create mutation
    //But the server allows it, so we test it
    var createInput = new ModelIngestionCreateInput(
      _model.id,
      _project.id,
      "Starting processing",
      new(".NET test runner", "0.0.0", null, null)
    );
    var ingestion = await Sut.Create(createInput, TestContext.Current.CancellationToken);
    var res = await Sut.StartProcessing(
      new(ingestion.id, _project.id, "", new SourceDataInput("what", "happens", "now", 0)),
      TestContext.Current.CancellationToken
    );

    Assert.Equal(ingestion.id, res.id);
    Assert.Equal(ModelIngestionStatus.processing, res.statusData.status);
  }
}
