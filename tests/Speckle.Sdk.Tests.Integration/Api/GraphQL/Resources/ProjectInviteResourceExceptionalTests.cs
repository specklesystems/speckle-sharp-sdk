using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Resources;

namespace Speckle.Sdk.Tests.Integration.API.GraphQL.Resources;

[TestOf(typeof(ProjectInviteResource))]
public class ProjectInviteResourceExceptionalTests
{
  private Client _testUser;
  private Project _project;
  private ProjectInviteResource Sut => _testUser.ProjectInvite;

  [OneTimeSetUp]
  public async Task Setup()
  {
    _testUser = await Fixtures.SeedUserWithClient();
    _project = await _testUser.Project.Create(new("test", null, null));
  }

  [TestCase(null, null, null, null)]
  [TestCase(null, "something", "something", null)]
  public void ProjectInviteCreate_InvalidInput(string email, string role, string serverRole, string userId)
  {
    var ex = Assert.CatchAsync<AggregateException>(async () =>
    {
      var input = new ProjectInviteCreateInput(email, role, serverRole, userId);
      await Sut.Create(_project.id, input);
    });
    Assert.That(ex?.InnerExceptions, Has.One.Items.And.All.TypeOf<SpeckleGraphQLException>());
  }
}
