using FluentAssertions;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Resources;

namespace Speckle.Sdk.Tests.Integration.API.GraphQL.Resources;

public class ServerResourceTests : IAsyncLifetime
{
  private IClient _testUser;
  private ServerResource Sut => _testUser.Server;

  public async Task InitializeAsync()
  {
    // Runs instead of [SetUp] in NUnit
    _testUser = await Fixtures.SeedUserWithClient();
  }

  public Task DisposeAsync()
  {
    // Perform any cleanup, if needed
    return Task.CompletedTask;
  }

  [Fact]
  public async Task ExpectWorkspaceNotEnabled()
  {
    bool result = await Sut.IsWorkspaceEnabled();
    result.Should().Be(true);
  }
}
