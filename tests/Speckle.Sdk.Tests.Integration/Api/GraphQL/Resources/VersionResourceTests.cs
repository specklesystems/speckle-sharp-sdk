using AwesomeAssertions;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Resources;
using Version = Speckle.Sdk.Api.GraphQL.Models.Version;

namespace Speckle.Sdk.Tests.Integration.API.GraphQL.Resources;

public class VersionResourceTests : IAsyncLifetime
{
  private IClient _testUser;
  private VersionResource Sut => _testUser.Version;
  private Project _project;
  private Model _model1;
  private Model _model2;
  private Version _version;

  public Task DisposeAsync() => Task.CompletedTask;

  public async Task InitializeAsync()
  {
    _testUser = await Fixtures.SeedUserWithClient();
    _project = await _testUser.Project.Create(new("Test project", "", null));
    _model1 = await _testUser.Model.Create(new("Test Model 1", "", _project.id));
    _model2 = await _testUser.Model.Create(new("Test Model 2", "", _project.id));

    _version = await Fixtures.CreateVersion(_testUser, _project.id, _model1.id);
  }

  [Fact]
  public async Task VersionGet()
  {
    Version result = await Sut.Get(_version.id, _project.id);

    result.id.Should().Be(_version.id);
    result.message.Should().Be(_version.message);
  }

  [Fact]
  public async Task VersionsGet()
  {
    ResourceCollection<Version> result = await Sut.GetVersions(_model1.id, _project.id);

    result.items.Count.Should().Be(1);
    result.totalCount.Should().Be(1);
    result.items[0].id.Should().Be(_version.id);
  }

  [Fact]
  public async Task VersionReceived()
  {
    MarkReceivedVersionInput input = new(_version.id, _project.id, "Integration test");
    await Sut.Received(input);
  }

  [Fact]
  public async Task ModelGetWithVersions()
  {
    var result = await _testUser.Model.GetWithVersions(_model1.id, _project.id);

    result.id.Should().Be(_model1.id);
    result.versions.items.Count.Should().Be(1);
    result.versions.totalCount.Should().Be(1);
    result.versions.items[0].id.Should().Be(_version.id);
  }

  [Fact]
  public async Task VersionUpdate()
  {
    const string NEW_MESSAGE = "MY new version message";

    UpdateVersionInput input = new(_version.id, _project.id, NEW_MESSAGE);
    Version updatedVersion = await Sut.Update(input);

    updatedVersion.id.Should().Be(_version.id);
    updatedVersion.message.Should().Be(NEW_MESSAGE);
    updatedVersion.previewUrl.Should().Be(_version.previewUrl);
  }

  [Fact]
  public async Task VersionMoveToModel()
  {
    MoveVersionsInput input = new(_project.id, _model2.name, [_version.id]);
    string id = await Sut.MoveToModel(input);

    id.Should().Be(_model2.id);

    Version movedVersion = await Sut.Get(_version.id, _project.id);

    movedVersion.id.Should().Be(_version.id);
    movedVersion.message.Should().Be(_version.message);
    movedVersion.previewUrl.Should().Be(_version.previewUrl);
  }

  [Fact]
  public async Task VersionDelete()
  {
    DeleteVersionsInput input = new([_version.id], _project.id);

    await Sut.Delete(input);

    var getEx = await FluentActions
      .Invoking(async () => await Sut.Get(_version.id, _project.id))
      .Should()
      .ThrowAsync<AggregateException>();
    getEx.WithInnerExceptionExactly<SpeckleGraphQLException>();

    var delEx = await FluentActions
      .Invoking(async () => await Sut.Delete(input))
      .Should()
      .ThrowAsync<AggregateException>();
    delEx.WithInnerExceptionExactly<SpeckleGraphQLException>();
  }
}
