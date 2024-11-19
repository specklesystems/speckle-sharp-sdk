using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Resources;

namespace Speckle.Sdk.Tests.Integration.API.GraphQL.Resources;

[TestOf(typeof(ModelResource))]
public class ModelResourceTests
{
  private Client _testUser;
  private ModelResource Sut => _testUser.Model;
  private Project _project;
  private Model _model;

  [SetUp]
  public async Task Setup()
  {
    _testUser = await Fixtures.SeedUserWithClient();
    _project = await _testUser.Project.Create(new("Test project", "", null));
    _model = await _testUser.Model.Create(new("Test Model", "", _project.id));
  }

  [Order(1)]
  [TestCase("My Model", "My model description")]
  [TestCase("my/nested/model", null)]
  public async Task ModelCreate(string name, string description)
  {
    CreateModelInput input = new(name, description, _project.id);
    Model result = await Sut.Create(input);

    Assert.That(result, Is.Not.Null);
    Assert.That(result, Has.Property(nameof(result.id)).Not.Null);
    Assert.That(result, Has.Property(nameof(result.name)).EqualTo(input.name).IgnoreCase);
    Assert.That(result, Has.Property(nameof(result.description)).EqualTo(input.description));
  }

  [Test]
  public async Task ModelGet()
  {
    Model result = await Sut.Get(_model.id, _project.id);

    Assert.That(result.id, Is.EqualTo(_model.id));
    Assert.That(result.name, Is.EqualTo(_model.name));
    Assert.That(result.description, Is.EqualTo(_model.description));
    Assert.That(result.createdAt, Is.EqualTo(_model.createdAt));
    Assert.That(result.updatedAt, Is.EqualTo(_model.updatedAt));
  }

  [Test]
  [Order(2)]
  public async Task GetModels()
  {
    var result = await Sut.GetModels(_project.id);

    Assert.That(result.items, Has.Count.EqualTo(1));
    Assert.That(result.totalCount, Is.EqualTo(1));
    Assert.That(result.items[0], Has.Property(nameof(Model.id)).EqualTo(_model.id));
  }

  [Test]
  public async Task Project_GetModels()
  {
    var result = await _testUser.Project.GetWithModels(_project.id);

    Assert.That(result, Has.Property(nameof(Project.id)).EqualTo(_project.id));
    Assert.That(result.models.items, Has.Count.EqualTo(1));
    Assert.That(result.models.totalCount, Is.EqualTo(1));
    Assert.That(result.models.items[0], Has.Property(nameof(Model.id)).EqualTo(_model.id));
  }

  [Test]
  public async Task ModelUpdate()
  {
    const string NEW_NAME = "MY new name";
    const string NEW_DESCRIPTION = "MY new desc";

    UpdateModelInput input = new(_model.id, NEW_NAME, NEW_DESCRIPTION, _project.id);
    Model updatedModel = await Sut.Update(input);

    Assert.That(updatedModel.id, Is.EqualTo(_model.id));
    Assert.That(updatedModel.name, Is.EqualTo(NEW_NAME).IgnoreCase);
    Assert.That(updatedModel.description, Is.EqualTo(NEW_DESCRIPTION));
    Assert.That(updatedModel.updatedAt, Is.GreaterThanOrEqualTo(_model.updatedAt));
  }

  [Test]
  public async Task ModelDelete()
  {
    DeleteModelInput input = new(_model.id, _project.id);

    await Sut.Delete(input);

    var getEx = Assert.CatchAsync<AggregateException>(async () => await Sut.Get(_model.id, _project.id));
    Assert.That(getEx?.InnerExceptions, Has.One.Items.And.All.TypeOf<SpeckleGraphQLException>());

    var delEx = Assert.CatchAsync<AggregateException>(async () => await Sut.Delete(input));
    Assert.That(delEx?.InnerExceptions, Has.One.Items.And.All.TypeOf<SpeckleGraphQLException>());
  }
}
