using FluentAssertions;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Resources;
using Speckle.Sdk.Api.GraphQL.Serializer;

namespace Speckle.Sdk.Tests.Integration.API.GraphQL.Resources;

public class ActiveUserResourceTests : IAsyncLifetime
{
  private IClient _testUser;
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
    res.Should().NotBeNull();
    res!.id.Should().Be(_testUser.Account.userInfo.id);
  }

  [Fact]
  public async Task ActiveUserGet_NonAuthed()
  {
    var result = await Fixtures.Unauthed.ActiveUser.Get();
    result.Should().BeNull();
  }

  [Fact]
  public async Task ActiveUserUpdate()
  {
    const string NEW_NAME = "Ron";
    const string NEW_BIO = "Now I have a bio, isn't that nice!";
    const string NEW_COMPANY = "Limited Cooperation Organization Inc";

    var res = await Sut.Update(new UserUpdateInput(name: NEW_NAME, bio: NEW_BIO, company: NEW_COMPANY));

    res.Should().NotBeNull();
    res.id.Should().Be(_testUser.Account.userInfo.id);
    res.name.Should().Be(NEW_NAME);
    res.company.Should().Be(NEW_COMPANY);
    res.bio.Should().Be(NEW_BIO);
  }

  [Fact]
  public async Task ActiveUserGetProjects()
  {
    var p1 = await _testUser.Project.Create(new("Project 1", null, null));
    var p2 = await _testUser.Project.Create(new("Project 2", null, null));

    var res = await Sut.GetProjects();

    res.items.Should().Contain(x => x.id == p1.id);
    res.items.Should().Contain(x => x.id == p2.id);
    res.items.Count.Should().Be(2);
  }

  [Fact]
  public async Task ActiveUserGetProjectsWithPermissions()
  {
    var p1 = await _testUser.Project.Create(new("Project 3", null, null));
    var p2 = await _testUser.Project.Create(new("Project 4", null, null));

    var res = await Sut.GetProjectsWithPermissions();

    res.items.Should().Contain(x => x.id == p1.id);
    res.items.Should().Contain(x => x.id == p2.id);
    res.items.Count.Should().Be(2);
  }

  [Fact]
  public async Task ActiveUserGetProjects_NoAuth()
  {
    await FluentActions
      .Invoking(async () => await Fixtures.Unauthed.ActiveUser.GetProjects())
      .Should()
      .ThrowAsync<SpeckleException>();
  }

  [Fact]
  public async Task ActiveUserProjectCreationPermission()
  {
    var res = await Sut.CanCreatePersonalProjects();
    res.EnsureAuthorised();

    res.authorized.Should().Be(true);
  }

  [Fact]
  public async Task ActiveUserProjectCreationPermission_NoAuth()
  {
    await FluentActions
      .Invoking(async () => await Fixtures.Unauthed.ActiveUser.CanCreatePersonalProjects())
      .Should()
      .ThrowAsync<SpeckleException>();
  }

  [Fact]
  public async Task ActiveUserGetWorkspaces()
  {
    var ex = await Assert.ThrowsAsync<AggregateException>(async () => _ = await Sut.GetWorkspaces());
    await Verify(ex);
  }

  [Fact]
  public async Task ActiveUserGetWorkspaces_NoAuth()
  {
    await FluentActions
      .Invoking(async () => await Fixtures.Unauthed.ActiveUser.GetWorkspaces())
      .Should()
      .ThrowAsync<SpeckleException>();
  }

  [Fact]
  public async Task ActiveUserGetActiveWorkspace()
  {
    var res = await Sut.GetActiveWorkspace();
    res.Should().Be(null);
  }

  [Fact]
  public async Task ActiveUserGetActiveWorkspace_NoAuth()
  {
    await FluentActions
      .Invoking(async () => await Fixtures.Unauthed.ActiveUser.GetActiveWorkspace())
      .Should()
      .ThrowAsync<SpeckleException>();
  }

  /// <summary>
  /// Frequently, When the server makes a change in the graphql schema to add a new property to an input
  /// object like <see cref="UserProjectsFilter"/>, we would like to implement that change in the SDK
  /// in a way that is non-breaking with older servers... Apollo will respond with a BAD_USER_INPUT otherwise
  ///
  /// To do that, the <see cref="Client"/> overrides the <see cref="NewtonsoftJsonSerializer"/>
  /// to ensure that `null` values are not serialized
  /// Since Apollo also treats a <c>null</c> value the same as no value at all, the server does not complain.
  ///
  /// This test emulates there being a property on an input that the server doesn't understand.
  /// And we expect it to not complain when the value is <see langword="null"/>
  /// </summary>
  [Fact]
  public async Task RequestWithNewerInput()
  {
    var filterNull = new FakeProjectInput(null);
    _ = await Sut.GetProjects(filter: filterNull);

    var filterNotNull = new FakeProjectInput("fake value");
    var ex = await Assert.ThrowsAsync<AggregateException>(async () =>
    {
      _ = await Sut.GetProjects(filter: filterNotNull);
    });
    ex.InnerExceptions.Single().Should().BeOfType<SpeckleGraphQLBadInputException>();
  }

  public record FakeProjectInput(string? fakeProperty) : UserProjectsFilter { }
}
