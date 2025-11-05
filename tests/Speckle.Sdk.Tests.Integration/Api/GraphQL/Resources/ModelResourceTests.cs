using FluentAssertions;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Resources;

namespace Speckle.Sdk.Tests.Integration.API.GraphQL.Resources;

public class ModelResourceTests : IAsyncLifetime
{
  private IClient _testUser;
  private ModelResource Sut => _testUser.Model;
  private Project _project;
  private Model _model;

  public async Task InitializeAsync()
  {
    // Runs instead of [SetUp] in NUnit
    _testUser = await Fixtures.SeedUserWithClient();
    _project = await _testUser.Project.Create(new("Test project", "", null));
    _model = await _testUser.Model.Create(new("Test Model", "", _project.id));
  }

  public Task DisposeAsync()
  {
    // Perform any cleanup, if needed
    return Task.CompletedTask;
  }

  [Theory]
  [InlineData("My Model", "My model description")]
  [InlineData("my/nested/model", null)]
  public async Task ModelCreate(string name, string? description)
  {
    // Arrange
    CreateModelInput input = new(name, description, _project.id);

    // Act
    Model result = await Sut.Create(input);

    // Assert
    result.Should().NotBeNull();
    result.id.Should().NotBeNull();
    result.name.Should().ContainEquivalentOf(input.name);
    result.description.Should().Be(input.description);
  }

  [Fact]
  public async Task ModelGet()
  {
    // Act
    Model result = await Sut.Get(_model.id, _project.id);

    // Assert
    result.id.Should().Be(_model.id);
    result.name.Should().Be(_model.name);
    result.description.Should().Be(_model.description);
    result.createdAt.Should().Be(_model.createdAt);
    result.updatedAt.Should().Be(_model.updatedAt);
  }

  [Fact]
  public async Task GetModels()
  {
    // Act
    var result = await Sut.GetModels(_project.id);

    // Assert
    result.items.Count.Should().Be(1);
    result.items[0].id.Should().Be(_model.id);
  }

  [Fact]
  public async Task Project_GetModels()
  {
    // Act
    var result = await _testUser.Project.GetWithModels(_project.id);

    // Assert
    result.id.Should().Be(_project.id);
    result.models.items.Count.Should().Be(1);
    result.models.items[0].id.Should().Be(_model.id);
  }

  [Fact]
  public async Task ModelUpdate()
  {
    // Arrange
    const string NEW_NAME = "MY new name";
    const string NEW_DESCRIPTION = "MY new desc";

    var input = new UpdateModelInput(_model.id, NEW_NAME, NEW_DESCRIPTION, _project.id);

    // Act
    Model updatedModel = await Sut.Update(input);

    // Assert
    updatedModel.id.Should().Be(_model.id);
    updatedModel.name.Should().ContainEquivalentOf(NEW_NAME);
    updatedModel.description.Should().Be(NEW_DESCRIPTION);
    updatedModel.updatedAt.Should().BeOnOrAfter(_model.updatedAt);
  }

  [Fact]
  public async Task ModelDelete()
  {
    // Arrange
    var input = new DeleteModelInput(_model.id, _project.id);

    // Act
    await Sut.Delete(input);

    // Assert: Ensure fetching the deleted model throws an exception
    var getEx = await FluentActions
      .Invoking(() => Sut.Get(_model.id, _project.id))
      .Should()
      .ThrowAsync<AggregateException>();
    getEx.WithInnerExceptionExactly<SpeckleGraphQLException>();

    // Assert: Ensure deleting the non-existing model again throws an exception
    var delEx = await FluentActions.Invoking(() => Sut.Delete(input)).Should().ThrowAsync<AggregateException>();
    getEx.WithInnerExceptionExactly<SpeckleGraphQLException>();
  }
}
