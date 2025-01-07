using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Credentials;
using Xunit;

namespace Speckle.Sdk.Tests.Unit.Credentials
{
  public class AccountServerMigrationTests : IDisposable
  {
    private readonly List<Account> _accountsToCleanUp = new();

    public static IEnumerable<object[]> MigrationTestCases()
    {
      const string OLD_URL = "https://old.example.com";
      const string NEW_URL = "https://new.example.com";
      const string OTHER_URL = "https://other.example.com";

      Account oldAccount = CreateTestAccount(OLD_URL, null, new(NEW_URL));
      string accountId = oldAccount.userInfo.id; // new account user must match old account user id
      Account newAccount = CreateTestAccount(NEW_URL, new(OLD_URL), null, accountId);
      Account otherAccount = CreateTestAccount(OTHER_URL, null, null);

      List<Account> givenAccounts = new() { oldAccount, newAccount, otherAccount };

      yield return new object[] { givenAccounts, NEW_URL, new[] { newAccount } };

      yield return new object[] { givenAccounts, OLD_URL, new[] { newAccount } };

      var reversed = givenAccounts.AsEnumerable().Reverse().ToList();

      yield return new object[] { reversed, OLD_URL, new[] { newAccount } };
    }

    [Theory]
    [MemberData(nameof(MigrationTestCases))]
    public void TestServerMigration(IList<Account> accounts, string requestedUrl, IList<Account> expectedSequence)
    {
      // Add accounts to the local setup
      AddAccounts(accounts);

      var serviceProvider = TestServiceSetup.GetServiceProvider();
      var result = serviceProvider.GetRequiredService<IAccountManager>().GetAccounts(requestedUrl).ToList();

      // Assert the result using Shouldly
      result.Should().BeEquivalentTo(expectedSequence);
    }

    public void Dispose()
    {
      // Clean up accounts after each test
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
      foreach (var account in accounts)
      {
        _accountsToCleanUp.Add(account);
        Fixtures.UpdateOrSaveAccount(account);
      }
    }
  }
}
