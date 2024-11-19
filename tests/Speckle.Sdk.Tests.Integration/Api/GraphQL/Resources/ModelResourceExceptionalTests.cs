using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Enums;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Resources;

namespace Speckle.Sdk.Tests.Integration.API.GraphQL.Resources;

[TestOf(typeof(ModelResource))]
public class ModelResourceExceptionalTests
{
  private Client _testUser;
  private ModelResource Sut => _testUser.Model;
  private Project _project;
  private Model _model;

  [OneTimeSetUp]
  public async Task Setup()
  {
    _testUser = await Fixtures.SeedUserWithClient();
    _project = await _testUser.Project.Create(new("Test project", "", ProjectVisibility.Private));
    _model = await _testUser.Model.Create(new("Test Model", "", _project.id));
  }

  [TestCase("")]
  [TestCase(" ")]
  public void ModelCreate_Throws_InvalidInput(string name)
  {
    CreateModelInput input = new(name, null, _project.id);
    var ex = Assert.ThrowsAsync<AggregateException>(async () => await Sut.Create(input));
    Assert.That(ex?.InnerExceptions, Has.One.Items.And.All.TypeOf<SpeckleGraphQLException>());
  }

  [Test]
  public void ModelGet_Throws_NoAuth()
  {
    var ex = Assert.ThrowsAsync<AggregateException>(
      async () => await Fixtures.Unauthed.Model.Get(_model.id, _project.id)
    );
    Assert.That(ex?.InnerExceptions, Has.One.Items.And.All.TypeOf<SpeckleGraphQLForbiddenException>());
  }

  [Test]
  public void ModelGet_Throws_NonExistentModel()
  {
    var ex = Assert.ThrowsAsync<AggregateException>(async () => await Sut.Get("non existent model", _project.id));
    Assert.That(ex?.InnerExceptions, Has.One.Items.And.All.TypeOf<SpeckleGraphQLException>());
  }

  [Test]
  public void ModelGet_Throws_NonExistentProject()
  {
    var ex = Assert.ThrowsAsync<AggregateException>(async () => await Sut.Get(_model.id, "non existent project"));
    Assert.That(ex?.InnerExceptions, Has.One.Items.And.All.TypeOf<SpeckleGraphQLStreamNotFoundException>());
  }

  [Test]
  public void ModelUpdate_Throws_NonExistentModel()
  {
    UpdateModelInput input = new("non-existent model", "MY new name", "MY new desc", _project.id);

    var ex = Assert.ThrowsAsync<AggregateException>(async () => await Sut.Update(input));
    Assert.That(ex?.InnerExceptions, Has.One.Items.And.All.TypeOf<SpeckleGraphQLException>());
  }

  [Test]
  public void ModelUpdate_Throws_NonExistentProject()
  {
    UpdateModelInput input = new(_model.id, "MY new name", "MY new desc", "non-existent project");

    var ex = Assert.ThrowsAsync<AggregateException>(async () => await Sut.Update(input));
    Assert.That(ex?.InnerExceptions, Has.One.Items.And.All.TypeOf<SpeckleGraphQLForbiddenException>());
  }

  [Test]
  public void ModelUpdate_Throws_NonAuthProject()
  {
    UpdateModelInput input = new(_model.id, "MY new name", "MY new desc", _project.id);

    var ex = Assert.ThrowsAsync<AggregateException>(async () => await Fixtures.Unauthed.Model.Update(input));
    Assert.That(ex?.InnerExceptions, Has.One.Items.And.All.TypeOf<SpeckleGraphQLForbiddenException>());
  }

  [Test]
  public async Task ModelDelete_Throws_NoAuth()
  {
    Model toDelete = await Sut.Create(new("Delete me", null, _project.id));
    DeleteModelInput input = new(toDelete.id, _project.id);
    await Sut.Delete(input);

    var ex = Assert.ThrowsAsync<AggregateException>(async () => await Sut.Delete(input));
    Assert.That(ex?.InnerExceptions, Has.One.Items.And.All.TypeOf<SpeckleGraphQLException>());
  }
}
