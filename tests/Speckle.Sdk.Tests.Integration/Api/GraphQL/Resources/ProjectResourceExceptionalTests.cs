using FluentAssertions;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL;
using Speckle.Sdk.Api.GraphQL.Enums;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Resources;
using Speckle.Sdk.Common;

namespace Speckle.Sdk.Tests.Integration.API.GraphQL.Resources;

public class ProjectResourceExceptionalTests : IAsyncLifetime
{
  private IClient _testUser,
    _secondUser,
    _unauthedUser;
  private Project _testProject;
  private ProjectResource Sut => _testUser.Project;

  public Task DisposeAsync() => Task.CompletedTask;

  public async Task InitializeAsync()
  {
    _testUser = await Fixtures.SeedUserWithClient();
    _secondUser = await Fixtures.SeedUserWithClient();
    _unauthedUser = Fixtures.Unauthed;
    _testProject = await _testUser.Project.Create(new("test project123", "desc", null));
  }

  //We want to check the following cases
  // 1. User lacks permissions (without auth)
  // 2. Target (Project or user) doesn't exist)
  // 3. Cancellation
  // 4. Server doesn't exist (is down)
  //There's got to be a smarter way to parametrise these...

  [Fact]
  public async Task ProjectCreate_WithoutAuth()
  {
    ProjectCreateInput input = new(
      "The best project",
      "The best description for the best project",
      ProjectVisibility.Private
    );

    var ex = await Assert.ThrowsAsync<AggregateException>(async () => _ = await _unauthedUser.Project.Create(input));
    ex.InnerExceptions.Single().Should().BeOfType<SpeckleGraphQLForbiddenException>();
  }

  [Fact]
  public async Task ProjectGet_WithoutAuth()
  {
    ProjectCreateInput input = new("Private Stream", "A very private stream", ProjectVisibility.Private);

    Project privateStream = await Sut.Create(input);

    var ex = await Assert.ThrowsAsync<AggregateException>(async () =>
      _ = await _unauthedUser.Project.Get(privateStream.id)
    );
    ex.InnerExceptions.Single().Should().BeOfType<SpeckleGraphQLForbiddenException>();
  }

  [Fact]
  public async Task ProjectGet_NonExistentProject()
  {
    var ex = await Assert.ThrowsAsync<AggregateException>(async () => await Sut.Get("NonExistentProject"));
    ex.InnerExceptions.Single().Should().BeOfType<SpeckleGraphQLStreamNotFoundException>();
  }

  [Fact]
  public async Task ProjectUpdate_NonExistentProject()
  {
    var ex = await Assert.ThrowsAsync<AggregateException>(async () =>
      _ = await Sut.Update(new("NonExistentProject", "My new name"))
    );
    ex.InnerExceptions.Single().Should().BeOfType<SpeckleGraphQLStreamNotFoundException>();
  }

  [Fact]
  public async Task ProjectUpdate_NoAuth()
  {
    var ex = await Assert.ThrowsAsync<AggregateException>(async () =>
      _ = await _unauthedUser.Project.Update(new(_testProject.id, "My new name"))
    );
    ex.InnerExceptions.Single().Should().BeOfType<SpeckleGraphQLForbiddenException>();
  }

  [Fact]
  public async Task ProjectCreateInWorkspace_NonWorkspaceServer()
  {
    var ex = await Assert.ThrowsAsync<AggregateException>(async () =>
      _ = await _unauthedUser.Project.CreateInWorkspace(
        new(_testProject.id, "My new name", ProjectVisibility.Public, "NonExistentWorkspace")
      )
    );
    ex.InnerExceptions.Single().Should().BeOfType<SpeckleGraphQLException>();
  }

  [Theory]
  [InlineData(StreamRoles.STREAM_OWNER)]
  [InlineData(StreamRoles.STREAM_CONTRIBUTOR)]
  [InlineData(StreamRoles.STREAM_REVIEWER)]
  [InlineData(StreamRoles.REVOKE)]
  public async Task ProjectUpdateRole_NonExistentProject(string? newRole)
  {
    ProjectUpdateRoleInput input = new(_secondUser.Account.id.NotNull(), "NonExistentProject", newRole);

    var ex = await Assert.ThrowsAsync<AggregateException>(async () => _ = await Sut.UpdateRole(input));
    ex.InnerExceptions.Single().Should().BeOfType<SpeckleGraphQLForbiddenException>();
  }

  [Theory]
  [InlineData(StreamRoles.STREAM_OWNER)]
  [InlineData(StreamRoles.STREAM_CONTRIBUTOR)]
  [InlineData(StreamRoles.STREAM_REVIEWER)]
  [InlineData(StreamRoles.REVOKE)]
  public async Task ProjectUpdateRole_NonAuth(string? newRole)
  {
    ProjectUpdateRoleInput input = new(_secondUser.Account.id.NotNull(), "NonExistentProject", newRole);

    var ex = await Assert.ThrowsAsync<AggregateException>(async () =>
      _ = await _unauthedUser.Project.UpdateRole(input)
    );
    ex.InnerExceptions.Single().Should().BeOfType<SpeckleGraphQLForbiddenException>();
  }

  [Fact]
  public async Task ProjectDelete_NonExistentProject()
  {
    await Sut.Delete(_testProject.id);

    var ex = await Assert.ThrowsAsync<AggregateException>(async () => _ = await Sut.Get(_testProject.id));
    ex.InnerExceptions.Single().Should().BeOfType<SpeckleGraphQLStreamNotFoundException>();
  }

  [Fact]
  public async Task ProjectInvites_NoAuth()
  {
    await Assert.ThrowsAsync<SpeckleException>(async () => await Fixtures.Unauthed.ActiveUser.GetProjectInvites());
  }
}
