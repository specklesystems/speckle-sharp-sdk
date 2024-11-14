using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Credentials;

namespace Speckle.Sdk.Tests.Unit.Credentials;

public class AccountServerMigrationTests : IDisposable
{
  private readonly List<Account> _accountsToCleanUp = new();

  public static IEnumerable<(IList<Account>, string, Account)> MigrationTestCase()
  {
    const string OLD_URL = "https://old.example.com";
    const string NEW_URL = "https://new.example.com";
    const string OTHER_URL = "https://other.example.com";
    Account oldAccount = CreateTestAccount(OLD_URL, null, new(NEW_URL));
    string accountId = oldAccount.userInfo.id; // new account user must match old account user id
    Account newAccount = CreateTestAccount(NEW_URL, new(OLD_URL), null, accountId);
    Account otherAccount = CreateTestAccount(OTHER_URL, null, null);

    List<Account> givenAccounts = new() { oldAccount, newAccount, otherAccount };

    yield return (givenAccounts, NEW_URL, newAccount);

    yield return (givenAccounts, OLD_URL, newAccount);

    var reversed = Enumerable.Reverse(givenAccounts).ToList();

    yield return (reversed, OLD_URL, newAccount);
  }

  [Test]
  [MethodDataSource(nameof(MigrationTestCase))]
  public void TestServerMigration(IList<Account> accounts, string requestedUrl, Account expected)
  {
    AddAccounts(accounts);
    var serviceProvider = TestServiceSetup.GetServiceProvider();

    var result = serviceProvider.GetRequiredService<IAccountManager>().GetAccounts(requestedUrl).ToList();

    result.ShouldContain(expected);
  }

  public void Dispose()
  {
    //Clean up any of the test accounts we made
    foreach (var acc in _accountsToCleanUp)
    {
      Fixtures.DeleteLocalAccount(acc.id);
    }
    _accountsToCleanUp.Clear();
  }

  private static Account CreateTestAccount(string url, Uri? movedFrom, Uri? movedTo, string? id = null)
  {
    id ??= Guid.NewGuid().ToString();
    return new Account
    {
      token = "myToken",
      serverInfo = new ServerInfo
      {
        url = url,
        name = "myServer",
        migration = new ServerMigration { movedTo = movedTo, movedFrom = movedFrom },
      },
      userInfo = new UserInfo
      {
        id = id,
        email = "user@example.com",
        name = "user",
      },
    };
  }

  private void AddAccounts(IEnumerable<Account> accounts)
  {
    foreach (Account account in accounts)
    {
      _accountsToCleanUp.Add(account);
      Fixtures.UpdateOrSaveAccount(account);
    }
  }
}
