using Speckle.Sdk.Api;
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

    var ex = Assert.ThrowsAsync<AggregateException>(async () => _ = await _unauthedUser.Project.Create(input));
    Assert.That(ex?.InnerExceptions, Has.One.Items.And.All.TypeOf<SpeckleGraphQLForbiddenException>());
  }

  [Test]
  public async Task ProjectGet_WithoutAuth()
  {
    ProjectCreateInput input = new("Private Stream", "A very private stream", ProjectVisibility.Private);

    Project privateStream = await Sut.Create(input);

    var ex = Assert.ThrowsAsync<AggregateException>(async () => _ = await _unauthedUser.Project.Get(privateStream.id));
    Assert.That(ex?.InnerExceptions, Has.One.Items.And.All.TypeOf<SpeckleGraphQLForbiddenException>());
  }

  [Test]
  public void ProjectGet_NonExistentProject()
  {
    var ex = Assert.ThrowsAsync<AggregateException>(async () => await Sut.Get("NonExistentProject"));
    Assert.That(ex?.InnerExceptions, Has.One.Items.And.All.TypeOf<SpeckleGraphQLStreamNotFoundException>());
  }

  [Test]
  public void ProjectUpdate_NonExistentProject()
  {
    var ex = Assert.ThrowsAsync<AggregateException>(
      async () => _ = await Sut.Update(new("NonExistentProject", "My new name"))
    );
    Assert.That(ex?.InnerExceptions, Has.One.Items.And.All.TypeOf<SpeckleGraphQLForbiddenException>());
  }

  [Test]
  public void ProjectUpdate_NoAuth()
  {
    var ex = Assert.ThrowsAsync<AggregateException>(
      async () => _ = await _unauthedUser.Project.Update(new(_testProject.id, "My new name"))
    );
    Assert.That(ex?.InnerExceptions, Has.One.Items.And.All.TypeOf<SpeckleGraphQLForbiddenException>());
  }

  [Test]
  [TestCase(StreamRoles.STREAM_OWNER)]
  [TestCase(StreamRoles.STREAM_CONTRIBUTOR)]
  [TestCase(StreamRoles.STREAM_REVIEWER)]
  [TestCase(StreamRoles.REVOKE)]
  public void ProjectUpdateRole_NonExistentProject(string newRole)
  {
    ProjectUpdateRoleInput input = new(_secondUser.Account.id, "NonExistentProject", newRole);

    var ex = Assert.ThrowsAsync<AggregateException>(async () => _ = await Sut.UpdateRole(input));
    Assert.That(ex?.InnerExceptions, Has.One.Items.And.All.TypeOf<SpeckleGraphQLForbiddenException>());
  }

  [Test]
  [TestCase(StreamRoles.STREAM_OWNER)]
  [TestCase(StreamRoles.STREAM_CONTRIBUTOR)]
  [TestCase(StreamRoles.STREAM_REVIEWER)]
  [TestCase(StreamRoles.REVOKE)]
  public void ProjectUpdateRole_NonAuth(string newRole)
  {
    ProjectUpdateRoleInput input = new(_secondUser.Account.id, "NonExistentProject", newRole);

    var ex = Assert.ThrowsAsync<AggregateException>(async () => _ = await _unauthedUser.Project.UpdateRole(input));
    Assert.That(ex?.InnerExceptions, Has.One.Items.And.All.TypeOf<SpeckleGraphQLForbiddenException>());
  }

  [Test]
  public async Task ProjectDelete_NonExistentProject()
  {
    bool response = await Sut.Delete(_testProject.id);
    Assert.That(response, Is.True);

    var ex = Assert.ThrowsAsync<AggregateException>(async () => _ = await Sut.Get(_testProject.id));
    Assert.That(ex?.InnerExceptions, Has.One.Items.And.All.TypeOf<SpeckleGraphQLStreamNotFoundException>());
  }

  [Test]
  public void ProjectInvites_NoAuth()
  {
    Assert.ThrowsAsync<SpeckleException>(async () => await Fixtures.Unauthed.ActiveUser.ProjectInvites());
  }
}
