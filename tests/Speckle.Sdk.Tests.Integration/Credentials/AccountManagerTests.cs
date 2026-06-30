using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Credentials;

namespace Speckle.Sdk.Tests.Integration.Credentials;

[Collection(nameof(RequiresSqLiteAccountDb))]
public class AccountManagerTests
{
  private IAccountManager _sut;

  public AccountManagerTests()
  {
    _sut = Fixtures.ServiceProvider.GetRequiredService<IAccountManager>();
  }

  [Fact]
  public async Task UpdateAccount_UpdatesUserInfo()
  {
    using IClient user = await Fixtures.SeedUserWithClient();
    string realAccountId = user.Account.id;
    UserInfo realUserData = user.Account.userInfo;
    UserInfo staleData = new()
    {
      avatar = "my old avatar",
      company = "my old company",
      email = "my.old.email@example.com",
      id = realUserData.id,
      name = "my old name",
    };
    // Mutate with "fake" data to simulate a stale account data
    user.Account.userInfo = staleData;
    user.Account.id = null!; //force re-generate id

    Assert.NotEqual(realAccountId, user.Account.id);

    await _sut.UpdateAccountInMemory(user.Account);

    Assert.Equal(realUserData.avatar, user.Account.userInfo.avatar);
    Assert.Equal(realUserData.company, user.Account.userInfo.company);
    Assert.Equal(realUserData.email, user.Account.userInfo.email);
    Assert.Equal(realUserData.id, user.Account.userInfo.id);
    Assert.Equal(realUserData.name, user.Account.userInfo.name);
    Assert.Equal(realAccountId, user.Account.id);
  }

  [Fact]
  public async Task UpdateAccount_UpdatesServerInfo()
  {
    using IClient user = await Fixtures.SeedUserWithClient();
    string realAccountId = user.Account.id;
    ServerInfo realServerData = user.Account.serverInfo;
    ServerInfo staleData = new()
    {
      company = "This old company",
      description = "this old description",
      name = "This old name",
      url = realServerData.url,
      version = "0.0.123",
    };
    // Mutate with "fake" data to simulate a stale account data
    user.Account.serverInfo = staleData;
    user.Account.id = null!; //force re-generate id

    Assert.Equal(realAccountId, user.Account.id); //account id should not change since we didn't change server url

    await _sut.UpdateAccountInMemory(user.Account);

    Assert.Equal(realServerData.company, user.Account.serverInfo.company);
    Assert.Equal(realServerData.description, user.Account.serverInfo.description);
    Assert.Equal(realServerData.name, user.Account.serverInfo.name);
    Assert.Equal(realServerData.url, user.Account.serverInfo.url);
    Assert.Equal(realServerData.version, user.Account.serverInfo.version);
    Assert.Equal(realAccountId, user.Account.id);
  }

  [Fact]
  public async Task UpdateAccount_ServerInfoMigration()
  {
    using IClient user = await Fixtures.SeedUserWithClient();
    string realAccountId = user.Account.id;
    ServerInfo realServerData = user.Account.serverInfo;
    ServerInfo staleData = new()
    {
      company = "This old company",
      description = "this old description",
      name = "This old name",
      url = realServerData.url,
      version = "0.0.123",
    };
    // Mutate with "fake" data to simulate a stale account data
    user.Account.serverInfo = staleData;
    user.Account.id = null!; //force re-generate id

    Assert.Equal(realAccountId, user.Account.id); //account id should not change since we didn't change server url

    await _sut.UpdateAccountInMemory(user.Account);

    Assert.Equal(realServerData.company, user.Account.serverInfo.company);
    Assert.Equal(realServerData.description, user.Account.serverInfo.description);
    Assert.Equal(realServerData.name, user.Account.serverInfo.name);
    Assert.Equal(realServerData.url, user.Account.serverInfo.url);
    Assert.Equal(realServerData.version, user.Account.serverInfo.version);
    Assert.Equal(realAccountId, user.Account.id);
  }
}
