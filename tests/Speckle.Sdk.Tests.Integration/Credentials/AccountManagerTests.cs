using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk.Credentials;

namespace Speckle.Sdk.Tests.Integration.Credentials;

public class AccountManagerTests
{
  private readonly IAccountManager _sut;

  public AccountManagerTests()
  {
    _sut = Fixtures.ServiceProvider.GetRequiredService<IAccountManager>();
  }

  [Fact]
  public async Task TestUpgradeAccount()
  {
    Account account = await Fixtures.SeedUser();
    var expectedName = account.userInfo.name;
    var expectedEmail = account.userInfo.email;
    var expectedCompany = account.userInfo.company;

    var expectedServerDescription = account.serverInfo.description;
    var expectedServerName = account.serverInfo.name;
    var expectedServerVersion = account.serverInfo.version;

    account.userInfo = new()
    {
      name = "abc",
      email = "abcde",
      company = "abcdefg",
    };

    account.serverInfo = new()
    {
      url = account.serverInfo.url,
      description = "old123123",
      name = "old123",
      version = "old",
    };

    await _sut.UpdateAccountInMemory(account);

    Assert.Equal(expectedName, account.userInfo.name);
    Assert.Equal(expectedEmail, account.userInfo.email);
    Assert.Equal(expectedCompany, account.userInfo.company);

    Assert.Equal(expectedServerDescription, account.serverInfo.description);
    Assert.Equal(expectedServerName, account.serverInfo.name);
    Assert.Equal(expectedServerVersion, account.serverInfo.version);
  }

  [Fact]
  public async Task TestTokenRefreshAccount()
  {
    Account account = await Fixtures.SeedUser();

    const string INVALID_TOKEN = "emulating an expired token";
    account.token = INVALID_TOKEN;
    await _sut.UpdateAccountInMemory(account);

    Assert.NotEqual(INVALID_TOKEN, account.token);
  }
}
