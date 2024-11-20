using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Resources;
using Version = Speckle.Sdk.Api.GraphQL.Models.Version;

namespace Speckle.Sdk.Tests.Integration.API.GraphQL.Resources;

[TestOf(typeof(VersionResource))]
public class VersionResourceTests
{
  private Client _testUser;
  private VersionResource Sut => _testUser.Version;
  private Project _project;
  private Model _model1;
  private Model _model2;
  private Version _version;

  [SetUp]
  public async Task Setup()
  {
    _testUser = await Fixtures.SeedUserWithClient();
    _project = await _testUser.Project.Create(new("Test project", "", null));
    _model1 = await _testUser.Model.Create(new("Test Model 1", "", _project.id));
    _model2 = await _testUser.Model.Create(new("Test Model 2", "", _project.id));

    string versionId = await Fixtures.CreateVersion(_testUser, _project.id, _model1.id);

    _version = await Sut.Get(versionId, _project.id);
  }

  [Test]
  public async Task VersionGet()
  {
    Version result = await Sut.Get(_version.id, _project.id);

    Assert.That(result, Has.Property(nameof(Version.id)).EqualTo(_version.id));
    Assert.That(result, Has.Property(nameof(Version.message)).EqualTo(_version.message));
  }

  [Test]
  public async Task VersionsGet()
  {
    ResourceCollection<Version> result = await Sut.GetVersions(_model1.id, _project.id);

    Assert.That(result.items, Has.Count.EqualTo(1));
    Assert.That(result.totalCount, Is.EqualTo(1));
    Assert.That(result.items[0], Has.Property(nameof(Version.id)).EqualTo(_version.id));
  }

  [Test]
  public async Task VersionReceived()
  {
    MarkReceivedVersionInput input = new(_version.id, _project.id, "Integration test");
    await Sut.Received(input);
  }

  [Test]
  public async Task ModelGetWithVersions()
  {
    var result = await _testUser.Model.GetWithVersions(_model1.id, _project.id);

    Assert.That(result, Has.Property(nameof(Model.id)).EqualTo(_model1.id));
    Assert.That(result.versions.items, Has.Count.EqualTo(1));
    Assert.That(result.versions.totalCount, Is.EqualTo(1));
    Assert.That(result.versions.items[0], Has.Property(nameof(Version.id)).EqualTo(_version.id));
  }

  [Test]
  public async Task VersionUpdate()
  {
    const string NEW_MESSAGE = "MY new version message";

    UpdateVersionInput input = new(_version.id, _project.id, NEW_MESSAGE);
    Version updatedVersion = await Sut.Update(input);

    Assert.That(updatedVersion, Has.Property(nameof(Version.id)).EqualTo(_version.id));
    Assert.That(updatedVersion, Has.Property(nameof(Version.message)).EqualTo(NEW_MESSAGE));
    Assert.That(updatedVersion, Has.Property(nameof(Version.previewUrl)).EqualTo(_version.previewUrl));
  }

  [Test]
  public async Task VersionMoveToModel()
  {
    MoveVersionsInput input = new(_project.id, _model2.name, [_version.id]);
    string id = await Sut.MoveToModel(input);
    Assert.That(id, Is.EqualTo(_model2.id));
    Version movedVersion = await Sut.Get(_version.id, _project.id);

    Assert.That(movedVersion, Has.Property(nameof(Version.id)).EqualTo(_version.id));
    Assert.That(movedVersion, Has.Property(nameof(Version.message)).EqualTo(_version.message));
    Assert.That(movedVersion, Has.Property(nameof(Version.previewUrl)).EqualTo(_version.previewUrl));
  }

  [Test]
  public async Task VersionDelete()
  {
    DeleteVersionsInput input = new([_version.id], _project.id);

    await Sut.Delete(input);

    var getEx = Assert.ThrowsAsync<AggregateException>(async () => await Sut.Get(_version.id, _project.id));
    Assert.That(getEx?.InnerExceptions, Has.Exactly(1).TypeOf<SpeckleGraphQLException>());
    var delEx = Assert.ThrowsAsync<AggregateException>(async () => await Sut.Delete(input));
    Assert.That(delEx?.InnerExceptions, Has.Exactly(1).TypeOf<SpeckleGraphQLException>());
  }
}
