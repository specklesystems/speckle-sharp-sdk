using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Resources;
using Xunit;
using Shouldly;

namespace Speckle.Sdk.Tests.Integration.API.GraphQL.Resources;

public class ActiveUserResourceTests : IAsyncLifetime
{
  private Client _testUser;
  private ActiveUserResource Sut => _testUser.ActiveUser;

  // Setup method for xUnit using IAsyncLifetime
  public async Task InitializeAsync()
  {
    _testUser = await Fixtures.SeedUserWithClient();
  }

  public Task DisposeAsync()
  {
    // No resources to dispose
    return Task.CompletedTask;
  }

  [Fact]
  public async Task ActiveUserGet()
  {
    var res = await Sut.Get();
    res.ShouldNotBeNull();
    res!.id.ShouldBe(_testUser.Account.userInfo.id);
  }

  [Fact]
  public async Task ActiveUserGet_NonAuthed()
  {
    var result = await Fixtures.Unauthed.ActiveUser.Get();
    result.ShouldBeNull();
  }

  [Fact]
  public async Task ActiveUserUpdate()
  {
    const string NEW_NAME = "Ron";
    const string NEW_BIO = "Now I have a bio, isn't that nice!";
    const string NEW_COMPANY = "Limited Cooperation Organization Inc";

    var res = await Sut.Update(new UserUpdateInput(name: NEW_NAME, bio: NEW_BIO, company: NEW_COMPANY));

    res.ShouldNotBeNull();
    res.id.ShouldBe(_testUser.Account.userInfo.id);
    res.name.ShouldBe(NEW_NAME);
    res.company.ShouldBe(NEW_COMPANY);
    res.bio.ShouldBe(NEW_BIO);
  }

  [Fact]
  public async Task ActiveUserGetProjects()
  {
    var p1 = await _testUser.Project.Create(new("Project 1", null, null));
    var p2 = await _testUser.Project.Create(new("Project 2", null, null));

    var res = await Sut.GetProjects();

    res.items.ShouldContain(x => x.id == p1.id);
    res.items.ShouldContain(x => x.id == p2.id);
    res.items.Count.ShouldBe(2);
  }

  [Fact]
  public async Task ActiveUserGetProjects_NoAuth()
  {
    await Should.ThrowAsync<SpeckleGraphQLException>(async () => await Fixtures.Unauthed.ActiveUser.GetProjects());
  }
}
