using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Resources;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports;
using Version = Speckle.Sdk.Api.GraphQL.Models.Version;

namespace Speckle.Sdk.Tests.Integration.API.GraphQL.Resources;

public sealed class ModelIngestionResourceTests : IAsyncLifetime
{
  private IClient _testUser;
  private ModelIngestionResource Sut => _testUser.Ingestion;
  private Project _project;
  private Model _model;
  private IOperations _operations;

  public Task DisposeAsync() => Task.CompletedTask;

  public async Task InitializeAsync()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, Assembly.GetExecutingAssembly());
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
    ModelIngestion ingest = await Sut.Create(createInput);

    var errorInput = new ModelIngestionFailedInput(ingest.id, _project.id, "A bad thing happened", "Over hear!");
    var res = await Sut.FailWithError(errorInput);
    Assert.Equal(ingest.id, res.id);
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
    ModelIngestion ingest = await Sut.Create(createInput);

    var input = new ModelIngestionCancelledInput(
      ingest.id,
      _project.id,
      cancellationMessage: "This was cancelled for testing purposes"
    );
    var res = await Sut.FailWithCancel(input);
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
    ModelIngestion ingest = await Sut.Create(createInput);

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
        _ = Sut.UpdateProgress(updateInput).Result;
      }),
      CancellationToken.None,
      new(true, true)
    );

    ModelIngestionSuccessInput finish = new(ingest.id, _project.id, sendResult.RootId);
    string versionId = await Sut.Complete(finish);
    Version version = await _testUser.Version.Get(versionId, _project.id);
    Assert.Equal(versionId, version.id);
    Assert.Equal(sendResult.RootId, version.referencedObject);
  }
}
