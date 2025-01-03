using Shouldly;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Resources;
using Xunit;

namespace Speckle.Sdk.Tests.Integration.API.GraphQL.Resources;

public class ModelResourceTests : IAsyncLifetime
{
  private Client _testUser;
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
    result.ShouldNotBeNull();
    result.id.ShouldNotBeNull();
    result.name.ShouldBe(input.name, StringCompareShould.IgnoreCase);
    result.description.ShouldBe(input.description);
  }

  [Fact]
  public async Task ModelGet()
  {
    // Act
    Model result = await Sut.Get(_model.id, _project.id);

    // Assert
    result.id.ShouldBe(_model.id);
    result.name.ShouldBe(_model.name);
    result.description.ShouldBe(_model.description);
    result.createdAt.ShouldBe(_model.createdAt);
    result.updatedAt.ShouldBe(_model.updatedAt);
  }

  [Fact]
  public async Task GetModels()
  {
    // Act
    var result = await Sut.GetModels(_project.id);

    // Assert
    result.items.Count.ShouldBe(1);
    result.totalCount.ShouldBe(1);
    result.items[0].id.ShouldBe(_model.id);
  }

  [Fact]
  public async Task Project_GetModels()
  {
    // Act
    var result = await _testUser.Project.GetWithModels(_project.id);

    // Assert
    result.id.ShouldBe(_project.id);
    result.models.items.Count.ShouldBe(1);
    result.models.totalCount.ShouldBe(1);
    result.models.items[0].id.ShouldBe(_model.id);
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
    updatedModel.id.ShouldBe(_model.id);
    updatedModel.name.ShouldBe(NEW_NAME, StringCompareShould.IgnoreCase);
    updatedModel.description.ShouldBe(NEW_DESCRIPTION);
    updatedModel.updatedAt.ShouldBeGreaterThanOrEqualTo(_model.updatedAt);
  }

  [Fact]
  public async Task ModelDelete()
  {
    // Arrange
    var input = new DeleteModelInput(_model.id, _project.id);

    // Act
    await Sut.Delete(input);

    // Assert: Ensure fetching the deleted model throws an exception
    var getEx = await Should.ThrowAsync<AggregateException>(() => Sut.Get(_model.id, _project.id));
    getEx.InnerExceptions.ShouldHaveSingleItem();
    getEx.InnerExceptions[0].ShouldBeOfType<SpeckleGraphQLException>();

    // Assert: Ensure deleting the non-existing model again throws an exception
    var delEx = await Should.ThrowAsync<AggregateException>(() => Sut.Delete(input));
    delEx.InnerExceptions.ShouldHaveSingleItem();
    delEx.InnerExceptions[0].ShouldBeOfType<SpeckleGraphQLException>();
  }
}
