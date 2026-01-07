using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Speckle.Automate.Sdk.Schema;
using Speckle.Automate.Sdk.Schema.Triggers;
using Speckle.Sdk;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Enums;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Common;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Models;
using Speckle.Sdk.Tests.Integration;
using Utils = Speckle.Automate.Sdk.Integration.TestAutomateUtils;

namespace Speckle.Automate.Sdk.Integration;

public sealed class AutomationContextTest : IAsyncLifetime
{
  private const string SERVER_SKIP_MESSAGE = "currently the function run cannot be integration tested with the server";
  private IOperations _operations;
  private IAutomationRunner _runner;
  private IClient _client;
  private Account _account;
  private IAutomationContextFactory _contextFactory;

  public async Task InitializeAsync()
  {
    var serviceCollection = new ServiceCollection();
    serviceCollection.AddAutomateSdk();
    var serviceProvider = serviceCollection.BuildServiceProvider();
    _account = await Fixtures.SeedUser().ConfigureAwait(false);
    _client = serviceProvider.GetRequiredService<IClientFactory>().Create(_account);
    _runner = serviceProvider.GetRequiredService<IAutomationRunner>();
    _operations = serviceProvider.GetRequiredService<IOperations>();
    _contextFactory = serviceProvider.GetRequiredService<IAutomationContextFactory>();
  }

  public Task DisposeAsync()
  {
    _client?.Dispose();
    return Task.CompletedTask;
  }

  private async Task<AutomationRunData> AutomationRunData(Base testObject)
  {
    Project project = await _client.Project.Create(new("Automate function e2e test", null, ProjectVisibility.Public));
    const string BRANCH_NAME = "Trigger";

    var model = await _client.Model.Create(new(BRANCH_NAME, null, project.id));
    string modelId = model.id;

    (string rootObjId, _) = await _operations.Send2(
      _client.ServerUrl,
      project.id,
      _client.Account.token,
      testObject,
      null,
      CancellationToken.None
    );

    var version = await _client.Version.Create(new(rootObjId, model.id, project.id));

    string automationName = Utils.RandomString(10);
    string automationId = Utils.RandomString(10);
    string automationRevisionId = Utils.RandomString(10);

    await Utils.RegisterNewAutomation(project.id, modelId, _client, automationId, automationName, automationRevisionId);

    string automationRunId = Utils.RandomString(10);
    string functionRunId = Utils.RandomString(10);

    var triggers = new List<VersionCreationTrigger> { new(modelId, version.id) };

    return new AutomationRunData
    {
      ProjectId = project.id,
      SpeckleServerUrl = _client.ServerUrl,
      AutomationId = automationId,
      AutomationRunId = automationRunId,
      FunctionRunId = functionRunId,
      Triggers = triggers,
    };
  }

  private VersionCreationTrigger GetVersionCreationTrigger(List<VersionCreationTrigger> triggers)
  {
    if (triggers.FirstOrDefault() is not VersionCreationTrigger trigger)
    {
      throw new Exception("Automation run data contained no valid triggers.");
    }

    return trigger;
  }

  [Fact(Skip = SERVER_SKIP_MESSAGE)]
  public async Task TestFunctionRun()
  {
    AutomationRunData automationRunData = await AutomationRunData(Utils.TestObject());
    IAutomationContext automationContext = await _runner.RunFunction(
      TestAutomateFunction.Run,
      automationRunData,
      _account.token.NotNull(),
      new TestFunctionInputs { ForbiddenSpeckleType = "Base" }
    );

    automationContext.RunStatus.Should().Be("FAILED");

    var trigger = GetVersionCreationTrigger(automationRunData.Triggers);

    AutomationStatus status = await AutomationStatusOperations.Get(
      automationRunData.ProjectId,
      trigger.Payload.ModelId,
      automationContext.SpeckleClient
    );

    status.Status.Should().Be(automationContext.RunStatus);
    string statusMessage = status.AutomationRuns[0].FunctionRuns[0].StatusMessage;

    statusMessage.Should().Be(automationContext.AutomationResult.StatusMessage);
  }

  [Fact(Skip = SERVER_SKIP_MESSAGE)]
  public void TestParseInputData()
  {
    const string FORBIDDEN_SPECKLE_TYPE = "Base";
    TestFunctionInputs testFunctionInputs = new() { ForbiddenSpeckleType = FORBIDDEN_SPECKLE_TYPE };
    FunctionRunData<TestFunctionInputs> functionRunData = new()
    {
      FunctionInputs = testFunctionInputs,
      SpeckleToken = "",
      AutomationRunData = default,
    };
    string serializedFunctionRunData = JsonConvert.SerializeObject(functionRunData);
    File.WriteAllText("./inputData.json", serializedFunctionRunData);
    FunctionRunData<TestFunctionInputs> data = FunctionRunDataParser.FromPath<TestFunctionInputs>("./inputData.json");

    data.FunctionInputs.ForbiddenSpeckleType.Should().Be(FORBIDDEN_SPECKLE_TYPE);
  }

  [Fact(Skip = SERVER_SKIP_MESSAGE)]
  public async Task TestFileUploads()
  {
    AutomationRunData automationRunData = await AutomationRunData(Utils.TestObject());
    IAutomationContext automationContext = await _contextFactory.Initialize(
      automationRunData,
      _account.token.NotNull()
    );

    string filePath = $"./{Utils.RandomString(10)}";
    await File.WriteAllTextAsync(filePath, "foobar");

    await automationContext.StoreFileResult(filePath);

    File.Delete(filePath);
    automationContext.AutomationResult.Blobs.Should().HaveCount(1);
  }

  [Fact(Skip = SERVER_SKIP_MESSAGE)]
  public async Task TestCreateVersionInProject()
  {
    AutomationRunData automationRunData = await AutomationRunData(Utils.TestObject());
    IAutomationContext automationContext = await _contextFactory.Initialize(
      automationRunData,
      _account.token.NotNull()
    );

    const string BRANCH_NAME = "test-branch";
    const string COMMIT_MSG = "automation test";

    var model = await automationContext.SpeckleClient.Model.Create(
      new(BRANCH_NAME, default, automationRunData.ProjectId)
    );

    await automationContext.CreateNewVersionInProject(Utils.TestObject(), model, COMMIT_MSG);

    var modelWithVersions = await automationContext.SpeckleClient.Model.GetWithVersions(
      model.id,
      automationRunData.ProjectId
    );

    modelWithVersions.versions.items[0].message.Should().Be(COMMIT_MSG);
  }

  [Fact(Skip = SERVER_SKIP_MESSAGE)]
  public async Task TestCreateVersionInProject_ThrowsErrorForSameModel()
  {
    AutomationRunData automationRunData = await AutomationRunData(Utils.TestObject());
    IAutomationContext automationContext = await _contextFactory.Initialize(
      automationRunData,
      _account.token.NotNull()
    );

    var trigger = GetVersionCreationTrigger(automationRunData.Triggers);

    var model = await automationContext.SpeckleClient.Model.Get(trigger.Payload.ModelId, automationRunData.ProjectId);

    const string COMMIT_MSG = "automation test";

    await Assert.ThrowsAsync<ArgumentException>(async () =>
    {
      await automationContext.CreateNewVersionInProject(Utils.TestObject(), model, COMMIT_MSG);
    });
  }

  [Fact(Skip = SERVER_SKIP_MESSAGE)]
  public async Task TestSetContextView()
  {
    AutomationRunData automationRunData = await AutomationRunData(Utils.TestObject());
    IAutomationContext automationContext = await _contextFactory.Initialize(
      automationRunData,
      _account.token.NotNull()
    );

    automationContext.SetContextView();

    var trigger = GetVersionCreationTrigger(automationRunData.Triggers);

    automationContext.AutomationResult.ResultView.Should().NotBeNull();
    string originModelView = $"{trigger.Payload.ModelId}@{trigger.Payload.VersionId}";

    automationContext.AutomationResult.ResultView.Should().EndWith($"models/{originModelView}");

    await automationContext.ReportRunStatus();
    const string DUMMY_CONTEXT = "foo@bar";

    automationContext.AutomationResult.ResultView = null;
    automationContext.SetContextView(new List<string> { DUMMY_CONTEXT }, true);

    automationContext.AutomationResult.ResultView.Should().NotBeNull();

    automationContext.AutomationResult.ResultView.Should().EndWith($"models/{originModelView},{DUMMY_CONTEXT}");

    await automationContext.ReportRunStatus();

    automationContext.AutomationResult.ResultView = null;
    automationContext.SetContextView(new List<string> { DUMMY_CONTEXT }, false);

    automationContext.AutomationResult.ResultView.Should().NotBeNull();
    automationContext.AutomationResult.ResultView.Should().EndWith($"models/{DUMMY_CONTEXT}");

    await automationContext.ReportRunStatus();

    automationContext.AutomationResult.ResultView = null;

    Assert.Throws<SpeckleException>(() =>
    {
      automationContext.SetContextView(null, false);
    });

    await automationContext.ReportRunStatus();
  }

  [Fact(Skip = SERVER_SKIP_MESSAGE)]
  public async Task TestReportRunStatus_Succeeded()
  {
    AutomationRunData automationRunData = await AutomationRunData(Utils.TestObject());
    IAutomationContext automationContext = await _contextFactory.Initialize(
      automationRunData,
      _account.token.NotNull()
    );

    automationContext.RunStatus.Should().Be(AutomationStatusMapping.Get(Schema.AutomationStatus.Running));

    automationContext.MarkRunSuccess("This is a success message");

    automationContext.RunStatus.Should().Be(AutomationStatusMapping.Get(Schema.AutomationStatus.Succeeded));
  }

  [Fact(Skip = SERVER_SKIP_MESSAGE)]
  public async Task TestReportRunStatus_Failed()
  {
    AutomationRunData automationRunData = await AutomationRunData(Utils.TestObject());
    IAutomationContext automationContext = await _contextFactory.Initialize(
      automationRunData,
      _account.token.NotNull()
    );

    automationContext.RunStatus.Should().Be(AutomationStatusMapping.Get(Schema.AutomationStatus.Running));

    string message = "This is a failure message";
    automationContext.MarkRunFailed(message);

    automationContext.RunStatus.Should().Be(AutomationStatusMapping.Get(Schema.AutomationStatus.Failed));
    automationContext.StatusMessage.Should().Be(message);
  }
}
