using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Resources;

namespace Speckle.Sdk.Tests.Integration.API.GraphQL.Resources;

[TestOf(typeof(ActiveUserResource))]
public class ActiveUserResourceTests
{
  private Client _testUser;
  private ActiveUserResource Sut => _testUser.ActiveUser;

  [OneTimeSetUp]
  public async Task Setup()
  {
    _testUser = await Fixtures.SeedUserWithClient();
  }

  [Test]
  public async Task ActiveUserGet()
  {
    var res = await Sut.Get();
    Assert.That(res, Is.Not.Null);
    Assert.That(res!.id, Is.EqualTo(_testUser.Account.userInfo.id));
  }

  [Test]
  public async Task ActiveUserGet_NonAuthed()
  {
    var result = await Fixtures.Unauthed.ActiveUser.Get();
    Assert.That(result, Is.EqualTo(null));
  }

  [Test]
  public async Task ActiveUserUpdate()
  {
    const string NEW_NAME = "Ron";
    const string NEW_BIO = "Now I have a bio, isn't that nice!";
    const string NEW_COMPANY = "Limited Cooperation Organization Inc";
    var res = await Sut.Update(new UserUpdateInput(name: NEW_NAME, bio: NEW_BIO, company: NEW_COMPANY));

    Assert.That(res, Is.Not.Null);
    Assert.That(res.id, Is.EqualTo(_testUser.Account.userInfo.id));
    Assert.That(res.name, Is.EqualTo(NEW_NAME));
    Assert.That(res.company, Is.EqualTo(NEW_COMPANY));
    Assert.That(res.bio, Is.EqualTo(NEW_BIO));
  }

  [Test]
  public async Task ActiveUserGetProjects()
  {
    var p1 = await _testUser.Project.Create(new("Project 1", null, null));
    var p2 = await _testUser.Project.Create(new("Project 2", null, null));

    var res = await Sut.GetProjects();

    Assert.That(res.items, Has.Exactly(1).Items.With.Property(nameof(Project.id)).EqualTo(p1.id));
    Assert.That(res.items, Has.Exactly(1).Items.With.Property(nameof(Project.id)).EqualTo(p2.id));
    Assert.That(res.items, Has.Count.EqualTo(2));
  }

  [Test]
  public void ActiveUserGetProjects_NoAuth()
  {
    Assert.ThrowsAsync<SpeckleGraphQLException>(async () => await Fixtures.Unauthed.ActiveUser.GetProjects());
  }
}
