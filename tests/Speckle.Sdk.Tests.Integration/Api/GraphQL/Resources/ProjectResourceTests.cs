using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Enums;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Resources;
using Speckle.Sdk.Tests.Integration;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace Speckle.Sdk.Tests.Integration.API.GraphQL.Resources;

public class ProjectResourceTests
{
  private readonly Client _testUser;
  private readonly Project _testProject;
  private ProjectResource Sut => _testUser.Project;

  public ProjectResourceTests()
  {
    var setupTask = Setup();
    setupTask.Wait(); // Ensure setup runs synchronously for the constructor
    (_testUser, _testProject) = setupTask.Result;
  }

  private async Task<(Client TestUser, Project TestProject)> Setup()
  {
    var testUser = await Fixtures.SeedUserWithClient();
    var testProject = await testUser.Project.Create(new ProjectCreateInput("test project123", "desc", null));
    return (testUser, testProject);
  }

  [Theory]
  [InlineData("Very private project", "My secret project", ProjectVisibility.Private)]
  [InlineData("Very public project", null, ProjectVisibility.Public)]
  public async Task ProjectCreate_Should_CreateProjectSuccessfully(
    string name,
    string? description,
    ProjectVisibility visibility)
  {
    // Arrange
    var input = new ProjectCreateInput(name, description, visibility);

    // Act
    var result = await Sut.Create(input);

    // Assert
    result.ShouldNotBeNull();
    result.id.ShouldNotBeNullOrWhiteSpace();
    result.name.ShouldBe(input.name);
    result.description.ShouldBe(input.description ?? string.Empty);
    result.visibility.ShouldBe(input.visibility.ShouldNotBeNull());
  }

  [Fact]
  public async Task ProjectGet_Should_ReturnCorrectProject()
  {
    // Act
    var result = await Sut.Get(_testProject.id);

    // Assert
    result.id.ShouldBe(_testProject.id);
    result.name.ShouldBe(_testProject.name);
    result.description.ShouldBe(_testProject.description);
    result.visibility.ShouldBe(_testProject.visibility);
    result.createdAt.ShouldBe(_testProject.createdAt);
  }

  [Fact]
  public async Task ProjectUpdate_Should_UpdateProjectSuccessfully()
  {
    // Arrange
    const string NEW_NAME = "MY new name";
    const string NEW_DESCRIPTION = "MY new desc";
    const ProjectVisibility NEW_VISIBILITY = ProjectVisibility.Public;

    // Act
    var newProject =
      await Sut.Update(new ProjectUpdateInput(_testProject.id, NEW_NAME, NEW_DESCRIPTION, null, NEW_VISIBILITY));

    // Assert
    newProject.id.ShouldBe(_testProject.id);
    newProject.name.ShouldBe(NEW_NAME);
    newProject.description.ShouldBe(NEW_DESCRIPTION);
    newProject.visibility.ShouldBe(NEW_VISIBILITY);
  }

  [Fact]
  public async Task ProjectDelete_Should_DeleteProjectSuccessfully()
  {
    // Arrange
    var toDelete = await Sut.Create(new ProjectCreateInput("Delete me", null, null));

    // Act
    await Sut.Delete(toDelete.id);

    // Assert
    var exception =
      await Should.ThrowAsync<SpeckleGraphQLStreamNotFoundException>(async () => await Sut.Get(toDelete.id));
    exception.ShouldNotBeNull();
  }
}
