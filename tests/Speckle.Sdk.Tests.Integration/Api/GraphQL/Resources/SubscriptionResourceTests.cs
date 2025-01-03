using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Enums;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Resources;
using Shouldly;
using Xunit;

namespace Speckle.Sdk.Tests.Integration.API.GraphQL.Resources;

public class SubscriptionResourceTests : IAsyncLifetime
{
  private const int WAIT_PERIOD = 300;
  private Client _testUser;
  private Project _testProject;
  private Model _testModel;
  private string _testVersion;

  private SubscriptionResource Sut => _testUser.Subscription;

  public Task DisposeAsync()
  {
    _testUser.Dispose();
    return Task.CompletedTask;
  }

  public async Task InitializeAsync()
  {
    _testUser = await Fixtures.SeedUserWithClient();
    _testProject = await _testUser.Project.Create(new("test project123", "desc", null));
    _testModel = await _testUser.Model.Create(new("test model", "desc", _testProject.id));
    _testVersion = await Fixtures.CreateVersion(_testUser, _testProject.id, _testModel.id);
  }

  [Fact]
  public async Task UserProjectsUpdated_SubscriptionIsCalled()
  {
    UserProjectsUpdatedMessage? subscriptionMessage = null;

    using var sub = Sut.CreateUserProjectsUpdatedSubscription();
    sub.Listeners += (_, message) => subscriptionMessage = message;

    await Task.Delay(WAIT_PERIOD); // Give time to subscription to be setup

    var created = await _testUser.Project.Create(new(null, null, null));

    await Task.Delay(WAIT_PERIOD); // Give time for subscription to be triggered

    subscriptionMessage.ShouldNotBeNull();
    subscriptionMessage!.id.ShouldBe(created.id);
    subscriptionMessage.type.ShouldBe(UserProjectsUpdatedMessageType.ADDED);
    subscriptionMessage.project.ShouldNotBeNull();
  }

  [Fact]
  public async Task ProjectModelsUpdated_SubscriptionIsCalled()
  {
    ProjectModelsUpdatedMessage? subscriptionMessage = null;

    using var sub = Sut.CreateProjectModelsUpdatedSubscription(_testProject.id);
    sub.Listeners += (_, message) => subscriptionMessage = message;

    await Task.Delay(WAIT_PERIOD); // Give time to subscription to be setup

    CreateModelInput input = new("my model", "myDescription", _testProject.id);
    var created = await _testUser.Model.Create(input);

    await Task.Delay(WAIT_PERIOD); // Give time for subscription to be triggered

    subscriptionMessage.ShouldNotBeNull();
    subscriptionMessage!.id.ShouldBe(created.id);
    subscriptionMessage.type.ShouldBe(ProjectModelsUpdatedMessageType.CREATED);
    subscriptionMessage.model.ShouldNotBeNull();
  }

  [Fact]
  public async Task ProjectUpdated_SubscriptionIsCalled()
  {
    ProjectUpdatedMessage? subscriptionMessage = null;

    using var sub = Sut.CreateProjectUpdatedSubscription(_testProject.id);
    sub.Listeners += (_, message) => subscriptionMessage = message;

    await Task.Delay(WAIT_PERIOD); // Give time to subscription to be setup

    var input = new ProjectUpdateInput(_testProject.id, "This is my new name");
    var created = await _testUser.Project.Update(input);

    await Task.Delay(WAIT_PERIOD); // Give time for subscription to be triggered

    subscriptionMessage.ShouldNotBeNull();
    subscriptionMessage!.id.ShouldBe(created.id);
    subscriptionMessage.type.ShouldBe(ProjectUpdatedMessageType.UPDATED);
    subscriptionMessage.project.ShouldNotBeNull();
  }

  [Fact]
  public async Task ProjectVersionsUpdated_SubscriptionIsCalled()
  {
    ProjectVersionsUpdatedMessage? subscriptionMessage = null;

    using var sub = Sut.CreateProjectVersionsUpdatedSubscription(_testProject.id);
    sub.Listeners += (_, message) => subscriptionMessage = message;

    await Task.Delay(WAIT_PERIOD); // Give time to subscription to be setup

    var created = await Fixtures.CreateVersion(_testUser, _testProject.id, _testModel.id);

    await Task.Delay(WAIT_PERIOD); // Give time for subscription to be triggered

    subscriptionMessage.ShouldNotBeNull();
    subscriptionMessage!.id.ShouldBe(created);
    subscriptionMessage.type.ShouldBe(ProjectVersionsUpdatedMessageType.CREATED);
    subscriptionMessage.version.ShouldNotBeNull();
  }

  [Fact]
  public async Task ProjectCommentsUpdated_SubscriptionIsCalled()
  {
    string resourceIdString = $"{_testProject.id},{_testModel.id},{_testVersion}";
    ProjectCommentsUpdatedMessage? subscriptionMessage = null;

    using var sub = Sut.CreateProjectCommentsUpdatedSubscription(new(_testProject.id, resourceIdString));
    sub.Listeners += (_, message) => subscriptionMessage = message;

    await Task.Delay(WAIT_PERIOD); // Give time to subscription to be setup

    var created = await Fixtures.CreateComment(_testUser, _testProject.id, _testModel.id, _testVersion);

    await Task.Delay(WAIT_PERIOD); // Give time for subscription to be triggered

    subscriptionMessage.ShouldNotBeNull();
    subscriptionMessage!.id.ShouldBe(created.id);
    subscriptionMessage.type.ShouldBe(ProjectCommentsUpdatedMessageType.CREATED);
    subscriptionMessage.comment.ShouldNotBeNull();
  }
}
