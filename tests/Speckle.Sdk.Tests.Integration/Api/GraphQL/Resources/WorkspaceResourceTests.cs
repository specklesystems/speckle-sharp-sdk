using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Resources;

namespace Speckle.Sdk.Tests.Integration.API.GraphQL.Resources;

public class WorkspaceResourceTests
{
  private readonly IClient _testUser;
  private WorkspaceResource Sut => _testUser.Workspace;

  public WorkspaceResourceTests()
  {
    var setupTask = Setup();
    setupTask.Wait(); // Ensure setup runs synchronously for the constructor
    _testUser = setupTask.Result;
  }

  private static async Task<IClient> Setup()
  {
    var testUser = await Fixtures.SeedUserWithClient();
    return testUser;
  }

  [Fact]
  public async Task TestGetWorkspace()
  {
    var ex = await Assert.ThrowsAsync<AggregateException>(async () => _ = await Sut.Get("non-existent-id"));
    await Verify(ex);
  }

  [Fact]
  public async Task TestGetProjects()
  {
    var ex = await Assert.ThrowsAsync<AggregateException>(async () => _ = await Sut.GetProjects("non-existent-id"));
    await Verify(ex);
  }
}
