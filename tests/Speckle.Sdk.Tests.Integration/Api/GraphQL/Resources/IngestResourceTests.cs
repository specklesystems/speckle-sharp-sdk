using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Resources;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Tests.Integration.API.GraphQL.Resources;

public class IngestResourceTests : IAsyncLifetime
{
  private IClient _testUser;
  private IngestResource Sut => _testUser.Ingest;
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
    var input = new IngestCreateInput(
      "myTestFile",
      1,
      _model.id,
      _project.id,
      ".NET",
      "0.0.0",
      new Dictionary<string, object?>()
    );
    Ingest ingest = await Sut.Create(input);

    var errorInput = new IngestErrorInput("A bad thing happened", "Over hear!", ingest.id, _project.id);
    var res = await Sut.Error(errorInput);
    Assert.True(res);

    var result = await Sut.GetIngests(_model.id, _project.id);

    await Verify(result);
  }

  [Fact]
  public async Task CreateAndCancel()
  {
    var input = new IngestCreateInput(
      "myTestFile",
      1,
      _model.id,
      _project.id,
      ".NET",
      "0.0.0",
      new Dictionary<string, object?>()
    );
    Ingest ingest = await Sut.Create(input);

    var errorInput = new CancelRequestInput(ingest.id, _project.id);
    var res = await Sut.Cancel(errorInput);
    Assert.True(res);

    var result = await Sut.GetIngests(_model.id, _project.id);

    await Verify(result);
  }

  [Fact]
  public async Task CreateAndEnd()
  {
    var create = new IngestCreateInput(
      "myTestFile",
      1,
      _model.id,
      _project.id,
      ".NET",
      "0.0.0",
      new Dictionary<string, object?>()
    );
    Ingest ingest = await Sut.Create(create);

    var myObject = Fixtures.GenerateNestedObject();
    var sendResult = await _operations.Send2(
      _testUser.ServerUrl,
      _project.id,
      _testUser.Account.token,
      myObject,
      new Progress<ProgressArgs>(x =>
      {
        var updateInput = new IngestUpdateInput(
          ingest.id,
          x.Total == null ? null : x.Count / x.Total,
          $"{x.Count} / {x.Total}",
          _project.id
        );
        _ = Sut.Update(updateInput).Result;
      }),
      CancellationToken.None,
      new(true, true)
    );

    var finish = new IngestFinishInput(ingest.id, "Yay! we completed", sendResult.RootId, _project.id);
    var res = await Sut.End(finish);
    Assert.NotNull(res);

    var result = await Sut.GetIngests(_model.id, _project.id);
    await Verify(result);
  }
}
