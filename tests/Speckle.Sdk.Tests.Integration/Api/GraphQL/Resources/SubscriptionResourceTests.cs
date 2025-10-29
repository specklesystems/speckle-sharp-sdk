﻿using FluentAssertions;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Enums;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Resources;
using Version = Speckle.Sdk.Api.GraphQL.Models.Version;

namespace Speckle.Sdk.Tests.Integration.API.GraphQL.Resources;

public class SubscriptionResourceTests : IAsyncLifetime
{
#if DEBUG
  private const int WAIT_PERIOD = 3000; // WSL is slow AF, so for local runs, we're being extra generous
#else
  private const int WAIT_PERIOD = 400; // For CI runs, a much smaller wait time is acceptable
#endif
  private const int TIMEOUT = WAIT_PERIOD + WAIT_PERIOD + 400;
  private IClient _testUser;
  private Project _testProject;
  private Model _testModel;
  private Version _testVersion;

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

  [Fact(Timeout = TIMEOUT)]
  public async Task UserProjectsUpdated_SubscriptionIsCalled()
  {
    TaskCompletionSource<UserProjectsUpdatedMessage> tcs = new();
    using var sub = Sut.CreateUserProjectsUpdatedSubscription();
    sub.Listeners += (_, message) => tcs.SetResult(message);

    await Task.Delay(WAIT_PERIOD); // Give time to subscription to be setup

    var created = await _testUser.Project.Create(new(null, null, null));

    var subscriptionMessage = await tcs.Task;

    subscriptionMessage.Should().NotBeNull();
    subscriptionMessage.id.Should().Be(created.id);
    subscriptionMessage.type.Should().Be(UserProjectsUpdatedMessageType.ADDED);
    subscriptionMessage.project.Should().NotBeNull();
  }

  [Fact(Timeout = TIMEOUT)]
  public async Task ProjectModelsUpdated_SubscriptionIsCalled()
  {
    TaskCompletionSource<ProjectModelsUpdatedMessage> tcs = new();
    using var sub = Sut.CreateProjectModelsUpdatedSubscription(_testProject.id);
    sub.Listeners += (_, message) => tcs.SetResult(message);

    await Task.Delay(WAIT_PERIOD); // Give time to subscription to be setup

    CreateModelInput input = new("my model", "myDescription", _testProject.id);
    var created = await _testUser.Model.Create(input);

    var subscriptionMessage = await tcs.Task;

    subscriptionMessage.Should().NotBeNull();
    subscriptionMessage.id.Should().Be(created.id);
    subscriptionMessage.type.Should().Be(ProjectModelsUpdatedMessageType.CREATED);
    subscriptionMessage.model.Should().NotBeNull();
  }

  [Fact(Timeout = TIMEOUT)]
  public async Task ProjectUpdated_SubscriptionIsCalled()
  {
    TaskCompletionSource<ProjectUpdatedMessage> tcs = new();
    using var sub = Sut.CreateProjectUpdatedSubscription(_testProject.id);
    sub.Listeners += (_, message) => tcs.SetResult(message);

    await Task.Delay(WAIT_PERIOD); // Give time to subscription to be setup

    var input = new ProjectUpdateInput(_testProject.id, "This is my new name");
    var created = await _testUser.Project.Update(input);

    var subscriptionMessage = await tcs.Task;

    subscriptionMessage.Should().NotBeNull();
    subscriptionMessage.id.Should().Be(created.id);
    subscriptionMessage.type.Should().Be(ProjectUpdatedMessageType.UPDATED);
    subscriptionMessage.project.Should().NotBeNull();
  }

  [Fact(Timeout = TIMEOUT)]
  public async Task ProjectVersionsUpdated_SubscriptionIsCalled()
  {
    TaskCompletionSource<ProjectVersionsUpdatedMessage> tcs = new();
    using var sub = Sut.CreateProjectVersionsUpdatedSubscription(_testProject.id);
    sub.Listeners += (_, message) => tcs.SetResult(message);

    await Task.Delay(WAIT_PERIOD); // Give time to subscription to be setup

    var created = await Fixtures.CreateVersion(_testUser, _testProject.id, _testModel.id);

    var subscriptionMessage = await tcs.Task;

    subscriptionMessage.Should().NotBeNull();
    subscriptionMessage.id.Should().Be(created.id);
    subscriptionMessage.type.Should().Be(ProjectVersionsUpdatedMessageType.CREATED);
    subscriptionMessage.version.Should().NotBeNull();
  }

  [Fact(Skip = CommentResourceTests.SERVER_SKIP_MESSAGE, Timeout = TIMEOUT)]
  public async Task ProjectCommentsUpdated_SubscriptionIsCalled()
  {
    string resourceIdString = $"{_testProject.id},{_testModel.id},{_testVersion}";

    TaskCompletionSource<ProjectCommentsUpdatedMessage> tcs = new();
    using var sub = Sut.CreateProjectCommentsUpdatedSubscription(new(_testProject.id, resourceIdString));
    sub.Listeners += (_, message) => tcs.SetResult(message);

    await Task.Delay(WAIT_PERIOD); // Give time to subscription to be setup

    var created = await Fixtures.CreateComment(_testUser, _testProject.id, _testModel.id, _testVersion.id);

    var subscriptionMessage = await tcs.Task;

    subscriptionMessage.Should().NotBeNull();
    subscriptionMessage.id.Should().Be(created.id);
    subscriptionMessage.type.Should().Be(ProjectCommentsUpdatedMessageType.CREATED);
    subscriptionMessage.comment.Should().NotBeNull();
  }
}
