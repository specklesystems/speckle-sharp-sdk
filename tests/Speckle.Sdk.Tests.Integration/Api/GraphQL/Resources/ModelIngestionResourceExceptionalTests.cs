using System.Reflection;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Resources;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Tests.Integration.API.GraphQL.Resources;

[Trait("Server", "Internal")]
public sealed class ModelIngestionResourceExceptionalTests : IAsyncLifetime
{
  private IClient _testUser;
  private ModelIngestionResource Sut => _testUser.Ingestion;
  private Project _project;
  private Model _model;

  public Task DisposeAsync() => Task.CompletedTask;

  public async Task InitializeAsync()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, Assembly.GetExecutingAssembly());

    _testUser = await Fixtures.SeedUserWithClient();
    _project = await _testUser.Project.Create(new("Test project", "", null));
    _model = await _testUser.Model.Create(new("Test Model 1", "", _project.id));
  }

  [Fact]
  public async Task CreateIngestionNonExistentProject()
  {
    var createInput = new ModelIngestionCreateInput(
      _model.id,
      "Doesn't exist...",
      "Starting processing",
      new(".NET test runner", "0.0.0", null, null)
    );

    var ex = await Assert.ThrowsAsync<AggregateException>(async () =>
    {
      _ = await Sut.Create(createInput);
    });
    Assert.Single(ex.InnerExceptions);
    Assert.All(ex.InnerExceptions, item => Assert.IsType<SpeckleGraphQLStreamNotFoundException>(item));
  }

  [Fact]
  public async Task UpdateNonExistentNonExistent()
  {
    var updateInput = new ModelIngestionUpdateInput("Doesn't exist", _project.id, "Can't be", 0.5);

    var ex = await Assert.ThrowsAsync<AggregateException>(async () =>
    {
      _ = await Sut.UpdateProgress(updateInput);
    });
    Assert.Single(ex.InnerExceptions);
    Assert.All(ex.InnerExceptions, item => Assert.IsType<SpeckleGraphQLException>(item));
  }

  [Fact]
  public async Task CancelNonExistentIngestion()
  {
    var input = new ModelIngestionCancelledInput(
      "Non-existent-ingestion",
      _project.id,
      cancellationMessage: "This was cancelled for testing purposes"
    );
    var ex = await Assert.ThrowsAsync<AggregateException>(async () =>
    {
      _ = await Sut.FailWithCancel(input);
    });
    Assert.Single(ex.InnerExceptions);
    Assert.All(ex.InnerExceptions, item => Assert.IsType<SpeckleGraphQLException>(item));
  }
}
