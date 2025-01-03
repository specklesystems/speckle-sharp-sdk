using Shouldly;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Enums;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Resources;
using Xunit;

namespace Speckle.Sdk.Tests.Integration.API.GraphQL.Resources;

public class ModelResourceExceptionalTests : IAsyncLifetime
{
  private Client _testUser;
  private ModelResource Sut => _testUser.Model;
  private Project _project;
  private Model _model;

  // Replaces NUnit's OneTimeSetUp with an async constructor logic or initializer pattern
  public async Task InitializeAsync()
  {
    _testUser = await Fixtures.SeedUserWithClient();
    _project = await _testUser.Project.Create(new("Test project", "", ProjectVisibility.Private));
    _model = await _testUser.Model.Create(new("Test Model", "", _project.id));
  }

  public Task DisposeAsync() => Task.CompletedTask;

  [Theory]
  [InlineData("")]
  [InlineData(" ")]
  public async Task ModelCreate_Throws_InvalidInput(string name)
  {
    // Arrange
    CreateModelInput input = new(name, null, _project.id);

    // Act & Assert
    var ex = await Should.ThrowAsync<AggregateException>(async () => await Sut.Create(input));

    ex.InnerExceptions.ShouldHaveSingleItem();
    ex.InnerExceptions[0].ShouldBeOfType<SpeckleGraphQLException>();
  }

  [Fact]
  public async Task ModelGet_Throws_NoAuth()
  {
    // Act & Assert
    var ex = await Should.ThrowAsync<AggregateException>(
      async () => await Fixtures.Unauthed.Model.Get(_model.id, _project.id)
    );

    ex.InnerExceptions.ShouldHaveSingleItem();
    ex.InnerExceptions[0].ShouldBeOfType<SpeckleGraphQLForbiddenException>();
  }

  [Fact]
  public async Task ModelGet_Throws_NonExistentModel()
  {
    // Act & Assert
    var ex = await Should.ThrowAsync<AggregateException>(async () => await Sut.Get("non existent model", _project.id));

    ex.InnerExceptions.ShouldHaveSingleItem();
    ex.InnerExceptions[0].ShouldBeOfType<SpeckleGraphQLException>();
  }

  [Fact]
  public async Task ModelGet_Throws_NonExistentProject()
  {
    // Act & Assert
    var ex = await Should.ThrowAsync<AggregateException>(async () => await Sut.Get(_model.id, "non existent project"));

    ex.InnerExceptions.ShouldHaveSingleItem();
    ex.InnerExceptions[0].ShouldBeOfType<SpeckleGraphQLStreamNotFoundException>();
  }

  [Fact]
  public async Task ModelUpdate_Throws_NonExistentModel()
  {
    // Arrange
    UpdateModelInput input = new("non-existent model", "MY new name", "MY new desc", _project.id);

    // Act & Assert
    var ex = await Should.ThrowAsync<AggregateException>(async () => await Sut.Update(input));

    ex.InnerExceptions.ShouldHaveSingleItem();
    ex.InnerExceptions[0].ShouldBeOfType<SpeckleGraphQLException>();
  }

  [Fact]
  public async Task ModelUpdate_Throws_NonExistentProject()
  {
    // Arrange
    UpdateModelInput input = new(_model.id, "MY new name", "MY new desc", "non-existent project");

    // Act & Assert
    var ex = await Should.ThrowAsync<AggregateException>(async () => await Sut.Update(input));

    ex.InnerExceptions.ShouldHaveSingleItem();
    ex.InnerExceptions[0].ShouldBeOfType<SpeckleGraphQLForbiddenException>();
  }

  [Fact]
  public async Task ModelUpdate_Throws_NonAuthProject()
  {
    // Arrange
    UpdateModelInput input = new(_model.id, "MY new name", "MY new desc", _project.id);

    // Act & Assert
    var ex = await Should.ThrowAsync<AggregateException>(async () => await Fixtures.Unauthed.Model.Update(input));

    ex.InnerExceptions.ShouldHaveSingleItem();
    ex.InnerExceptions[0].ShouldBeOfType<SpeckleGraphQLForbiddenException>();
  }

  [Fact]
  public async Task ModelDelete_Throws_NoAuth()
  {
    // Arrange
    Model toDelete = await Sut.Create(new("Delete me", null, _project.id));
    DeleteModelInput input = new(toDelete.id, _project.id);

    await Sut.Delete(input);

    // Act & Assert
    var ex = await Should.ThrowAsync<AggregateException>(async () => await Sut.Delete(input));

    ex.InnerExceptions.ShouldHaveSingleItem();
    ex.InnerExceptions[0].ShouldBeOfType<SpeckleGraphQLException>();
  }
}
