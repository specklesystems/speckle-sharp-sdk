﻿using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL;
using Speckle.Sdk.Api.GraphQL.Enums;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Resources;

namespace Speckle.Sdk.Tests.Integration.API.GraphQL.Resources;

[TestOf(typeof(ProjectResource))]
public class ProjectResourceExceptionalTests
{
  private Client _testUser,
    _secondUser,
    _unauthedUser;
  private Project _testProject;
  private ProjectResource Sut => _testUser.Project;

  [OneTimeSetUp]
  public async Task Setup()
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

  [Test]
  public void ProjectCreate_WithoutAuth()
  {
    ProjectCreateInput input =
      new("The best project", "The best description for the best project", ProjectVisibility.Private);

    Assert.ThrowsAsync<SpeckleGraphQLForbiddenException>(async () => await _unauthedUser.Project.Create(input));
  }

  [Test]
  public async Task ProjectGet_WithoutAuth()
  {
    ProjectCreateInput input = new("Private Stream", "A very private stream", ProjectVisibility.Private);

    Project privateStream = await Sut.Create(input);

    Assert.ThrowsAsync<SpeckleGraphQLForbiddenException>(async () => await _unauthedUser.Project.Get(privateStream.id));
  }

  [Test]
  public void ProjectGet_NonExistentProject()
  {
    Assert.ThrowsAsync<SpeckleGraphQLStreamNotFoundException>(async () => await Sut.Get("NonExistentProject"));
  }

  [Test]
  public void ProjectUpdate_NonExistentProject()
  {
    Assert.ThrowsAsync<SpeckleGraphQLForbiddenException>(
      async () => _ = await Sut.Update(new("NonExistentProject", "My new name"))
    );
  }

  [Test]
  public void ProjectUpdate_NoAuth()
  {
    Assert.ThrowsAsync<SpeckleGraphQLForbiddenException>(
      async () => _ = await _unauthedUser.Project.Update(new(_testProject.id, "My new name"))
    );
  }

  [Test]
  [TestCase(StreamRoles.STREAM_OWNER)]
  [TestCase(StreamRoles.STREAM_CONTRIBUTOR)]
  [TestCase(StreamRoles.STREAM_REVIEWER)]
  [TestCase(StreamRoles.REVOKE)]
  public void ProjectUpdateRole_NonExistentProject(string newRole)
  {
    ProjectUpdateRoleInput input = new(_secondUser.Account.id, "NonExistentProject", newRole);

    Assert.ThrowsAsync<SpeckleGraphQLForbiddenException>(async () => await Sut.UpdateRole(input));
  }

  [Test]
  [TestCase(StreamRoles.STREAM_OWNER)]
  [TestCase(StreamRoles.STREAM_CONTRIBUTOR)]
  [TestCase(StreamRoles.STREAM_REVIEWER)]
  [TestCase(StreamRoles.REVOKE)]
  public void ProjectUpdateRole_NonAuth(string newRole)
  {
    ProjectUpdateRoleInput input = new(_secondUser.Account.id, "NonExistentProject", newRole);
    Assert.ThrowsAsync<SpeckleGraphQLForbiddenException>(async () => await _unauthedUser.Project.UpdateRole(input));
  }

  [Test]
  public async Task ProjectDelete_NonExistentProject()
  {
    await Sut.Delete(_testProject.id);

    Assert.ThrowsAsync<SpeckleGraphQLStreamNotFoundException>(async () => _ = await Sut.Get(_testProject.id)); //TODO: Exception types
  }

  [Test]
  public void ProjectInvites_NoAuth()
  {
    Assert.ThrowsAsync<SpeckleGraphQLException>(async () => await Fixtures.Unauthed.ActiveUser.ProjectInvites());
  }
}
