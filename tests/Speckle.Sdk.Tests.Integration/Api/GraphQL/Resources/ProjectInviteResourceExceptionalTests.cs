using FluentAssertions;
using Shouldly;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Resources;
using Xunit;

namespace Speckle.Sdk.Tests.Integration.API.GraphQL.Resources;

public class ProjectInviteResourceExceptionalTests : IAsyncLifetime
{
  private Client _testUser;
  private Project _project;
  private ProjectInviteResource Sut => _testUser.ProjectInvite;

  // Replacing OneTimeSetUp with IAsyncLifetime's InitializeAsync
  public async Task InitializeAsync()
  {
    _testUser = await Fixtures.SeedUserWithClient();
    _project = await _testUser.Project.Create(new ProjectCreateInput("test", null, null));
  }

  // Implementing IAsyncLifetime's DisposeAsync (optional if no cleanup is needed)
  public Task DisposeAsync() => Task.CompletedTask;

  [Theory]
  [InlineData(null, null, null, null)]
  [InlineData(null, "something", "something", null)]
  public async Task ProjectInviteCreate_InvalidInput_ShouldThrowSpeckleGraphQLException(
    string? email,
    string? role,
    string? serverRole,
    string? userId
  )
  {
    var input = new ProjectInviteCreateInput(email, role, serverRole, userId);

    var exception = await Should.ThrowAsync<AggregateException>(async () =>
    {
      await Sut.Create(_project.id, input);
    });

    exception.InnerExceptions.Should().ContainSingle();
    exception.InnerExceptions[0].Should().BeOfType<SpeckleGraphQLException>();
  }
}
