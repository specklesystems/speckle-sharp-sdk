using FluentAssertions;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Enums;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Resources;

namespace Speckle.Sdk.Tests.Integration.API.GraphQL.Resources;

public class ProjectResourceTests
{
  private readonly IClient _testUser;
  private readonly Project _testProject;
  private ProjectResource Sut => _testUser.Project;

  public ProjectResourceTests()
  {
    var setupTask = Setup();
    setupTask.Wait(); // Ensure setup runs synchronously for the constructor
    (_testUser, _testProject) = setupTask.Result;
  }

  private static async Task<(IClient TestUser, Project TestProject)> Setup()
  {
    var testUser = await Fixtures.SeedUserWithClient();
    var testProject = await testUser.Project.Create(new ProjectCreateInput("test project123", "desc", null));
    return (testUser, testProject);
  }

  [Theory]
  [InlineData("Very private project", "My secret project", ProjectVisibility.Private)]
  [InlineData("Very unlisted project", null, ProjectVisibility.Public)]
  public async Task ProjectCreate_Should_CreateProjectSuccessfully(
    string name,
    string? description,
    ProjectVisibility visibility
  )
  {
    // Arrange
    var input = new ProjectCreateInput(name, description, visibility);

    // Act
    var result = await Sut.Create(input);

    // Assert
    result.Should().NotBeNull();
    result.id.Should().NotBeNullOrWhiteSpace();
    result.name.Should().Be(input.name);
    result.description.Should().Be(input.description ?? string.Empty);
    input.visibility.Should().NotBeNull();
  }

  [Fact]
  public async Task ProjectGet_Should_ReturnCorrectProject()
  {
    // Act
    var result = await Sut.Get(_testProject.id);

    // Assert
    result.id.Should().Be(_testProject.id);
    result.name.Should().Be(_testProject.name);
    result.description.Should().Be(_testProject.description);
    result.visibility.Should().Be(_testProject.visibility);
    result.createdAt.Should().Be(_testProject.createdAt);
  }

  [Fact]
  public async Task ProjectUpdate_Should_UpdateProjectSuccessfully()
  {
    // Arrange
    const string NEW_NAME = "MY new name";
    const string NEW_DESCRIPTION = "MY new desc";
    const ProjectVisibility NEW_VISIBILITY = ProjectVisibility.Public;

    // Act
    var newProject = await Sut.Update(
      new ProjectUpdateInput(_testProject.id, NEW_NAME, NEW_DESCRIPTION, null, NEW_VISIBILITY)
    );

    // Assert
    newProject.id.Should().Be(_testProject.id);
    newProject.name.Should().Be(NEW_NAME);
    newProject.description.Should().Be(NEW_DESCRIPTION);
    newProject.visibility.Should().Be(NEW_VISIBILITY);
  }

  [Fact]
  public async Task ProjectDelete_Should_DeleteProjectSuccessfully()
  {
    // Arrange
    var toDelete = await Sut.Create(new ProjectCreateInput("Delete me", null, null));

    // Act
    await Sut.Delete(toDelete.id);

    // Assert
    await FluentActions
      .Invoking(async () => await Sut.Get(toDelete.id))
      .Should()
      .ThrowAsync<SpeckleGraphQLStreamNotFoundException>();
  }

  [Fact]
  public async Task TestUserHasProjectPermissions()
  {
    var privateProject = await _testUser.Project.Create(
      new ProjectCreateInput("asdfasdf", "desc", ProjectVisibility.Private)
    );

    var resp = await Sut.GetPermissions(privateProject.id);
    resp.canCreateModel.authorized.Should().Be(true);
    resp.canDelete.authorized.Should().Be(true);
    resp.canLoad.authorized.Should().Be(true);

    var publicProject = await _testUser.Project.Create(
      new ProjectCreateInput("asdfasdf", "desc", ProjectVisibility.Public)
    );

    var res = await Sut.GetPermissions(publicProject.id);
    res.canCreateModel.authorized.Should().Be(true);
    res.canDelete.authorized.Should().Be(true);
    res.canLoad.authorized.Should().Be(true);

    // Test with another user
    var guest = await Fixtures.SeedUserWithClient();

    var guestResult = await guest.Project.GetPermissions(publicProject.id);
    guestResult.canCreateModel.authorized.Should().Be(false);
    guestResult.canDelete.authorized.Should().Be(false);
    guestResult.canLoad.authorized.Should().Be(false);
  }
}
