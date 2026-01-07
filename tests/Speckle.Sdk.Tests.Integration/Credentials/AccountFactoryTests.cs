using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Common;
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

  [Fact]
  public async Task CreateAccount()
  {
    var expectedServerInfo = _client.Account.serverInfo;
    var expectedUserInfo = _client.Account.userInfo;

    var result = await _sut.CreateAccount(_client.ServerUrl, _client.Account.token.NotNull());

    result.serverInfo.url.Should().Be(expectedServerInfo.url).And.NotBeNull();
    result.serverInfo.name.Should().Be(expectedServerInfo.name).And.NotBeNull();
    result.serverInfo.company.Should().Be(expectedServerInfo.company);
    result.serverInfo.description.Should().Be(expectedServerInfo.description);

    result.userInfo.name.Should().Be(expectedUserInfo.name).And.NotBeNull();
    result.userInfo.id.Should().Be(expectedUserInfo.id).And.NotBeNull();
    result.userInfo.email.Should().Be(expectedUserInfo.email).And.NotBeNull();
    result.userInfo.avatar.Should().Be(expectedUserInfo.avatar);
  }

  [Fact]
  public async Task CreateAccount_ExpectFail_NoUser()
  {
    var res = await Assert.ThrowsAsync<SpeckleException>(async () =>
      _ = await _sut.CreateAccount(_client.ServerUrl, null!)
    );

    await Verify(res);
  }

  [Fact]
  public async Task CreateAccount_ExpectFail_NoServer()
  {
    Uri server = new("https://non-existing-server.local");
    await Assert.ThrowsAsync<HttpRequestException>(async () =>
    {
      _ = await _sut.CreateAccount(server, "ASDFASDF");
    });
  }
}
