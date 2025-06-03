using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Credentials;

namespace Speckle.Sdk.Tests.Integration.Credentials;

public class AccountFactoryTests : IAsyncLifetime
{
  private IAccountFactory _sut;
  private IClient _client;

  public async Task InitializeAsync()
  {
    _sut = Fixtures.ServiceProvider.GetRequiredService<IAccountFactory>();
    _client = await Fixtures.SeedUserWithClient();
  }

  public Task DisposeAsync() => Task.CompletedTask;

  [Fact]
  public async Task GetUserServerInfo()
  {
    const string NAME = "new expected name";
    const string COMPANY = "new expected company";
    var input = new UserUpdateInput(company: COMPANY, name: NAME);
    await _client.ActiveUser.Update(input);

    var result = await _sut.GetUserServerInfo(_client.ServerUrl, _client.Account.token, CancellationToken.None);

    result.serverInfo.url.Should().Be(Fixtures.Server.url);
    result.activeUser.Should().NotBeNull();
    result.activeUser.name.Should().Be(NAME);
    result.activeUser.avatar.Should().Be(null);
    result.activeUser.company.Should().Be(COMPANY);
  }

  [Fact]
  public async Task GetUserInfo_ExpectFail_NoServer()
  {
    Uri server = new("https://non-existing-server.local");
    await Assert.ThrowsAsync<HttpRequestException>(async () =>
    {
      _ = await _sut.GetUserServerInfo(server, _client.Account.token, CancellationToken.None);
    });
  }

  [Fact]
  public async Task GetUserInfo_NoUser()
  {
    var result = await _sut.GetUserServerInfo(_client.ServerUrl, null, CancellationToken.None);

    result.serverInfo.url.Should().Be(Fixtures.Server.url);
    result.activeUser.Should().BeNull();
  }
}
