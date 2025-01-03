using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Credentials;
using Xunit;

namespace Speckle.Sdk.Tests.Unit.Credentials;

public class CredentialInfrastructure
{
  private readonly IAccountManager _accountManager;
  private static readonly Account s_testAccount1;
  private static readonly Account s_testAccount2;
  private static readonly Account s_testAccount3;

  static CredentialInfrastructure() // Equivalent of `OneTimeSetUp`
  {
    s_testAccount1 = new Account
    {
      refreshToken = "bla",
      token = "bla",
      serverInfo = new ServerInfo { url = "https://bla.example.com", company = "bla" },
      userInfo = new UserInfo { email = "one@two.com" },
    };

    s_testAccount2 = new Account
    {
      refreshToken = "foo",
      token = "bar",
      serverInfo = new ServerInfo { url = "https://baz.example.com", company = "qux" },
      userInfo = new UserInfo { email = "three@four.com" },
    };

    s_testAccount3 = new Account
    {
      token = "secret",
      serverInfo = new ServerInfo { url = "https://example.com", name = "qux" },
      userInfo = new UserInfo
      {
        email = "six@five.com",
        id = "123345",
        name = "Test Account 3",
      },
    };

    Fixtures.UpdateOrSaveAccount(s_testAccount1);
    Fixtures.UpdateOrSaveAccount(s_testAccount2);
    Fixtures.SaveLocalAccount(s_testAccount3);
  }

  public CredentialInfrastructure() // Equivalent of `SetUp`
  {
    var serviceProvider = TestServiceSetup.GetServiceProvider();
    _accountManager = serviceProvider.GetRequiredService<IAccountManager>();
  }

  [Fact]
  public void GetAllAccounts()
  {
    var accs = _accountManager.GetAccounts().ToList();
    accs.Count.ShouldBeGreaterThanOrEqualTo(3); // Tests are adding three accounts, there might be extra accounts locally
  }

  [Fact]
  public void GetAccount_ById()
  {
    var result = _accountManager.GetAccount(s_testAccount1.id);

    result.ShouldBe(s_testAccount1); // Uses `Shouldly` for a clean assertion
  }

  [Fact]
  public void GetAccount_ById_ThrowsWhenNotFound()
  {
    Should.Throw<SpeckleAccountManagerException>(() => _accountManager.GetAccount("Non_existent_id"));
  }

  public static IEnumerable<object[]> TestCases() // Replaces NUnit's TestCaseSource
  {
    return new List<object[]>
    {
      new object[] { s_testAccount1 },
      new object[] { s_testAccount2 },
      new object[] { s_testAccount3 },
    };
  }

  [Theory]
  [MemberData(nameof(TestCases))] // Replaces `TestCaseSource`
  public void GetAccountsForServer(Account target)
  {
    var accs = _accountManager.GetAccounts(target.serverInfo.url).ToList();

    accs.Count.ShouldBe(1);

    var acc = accs[0];

    acc.ShouldNotBeSameAs(target); // We expect new objects (no reference equality)
    acc.serverInfo.company.ShouldBe(target.serverInfo.company);
    acc.serverInfo.url.ShouldBe(target.serverInfo.url);
    acc.refreshToken.ShouldBe(target.refreshToken);
    acc.token.ShouldBe(target.token);
  }

  [Fact]
  public void EnsureLocalIdentifiers_AreUniqueAcrossServers()
  {
    // Accounts with the same user ID in different servers should always result in different local identifiers.
    string id = "12345";
    var acc1 = new Account
    {
      serverInfo = new ServerInfo { url = "https://speckle.xyz" },
      userInfo = new UserInfo { id = id },
    }.GetLocalIdentifier();

    var acc2 = new Account
    {
      serverInfo = new ServerInfo { url = "https://app.speckle.systems" },
      userInfo = new UserInfo { id = id },
    }.GetLocalIdentifier();

    acc1.ShouldNotBe(acc2);
  }

  ~CredentialInfrastructure() // Equivalent of `OneTimeTearDown`
  {
    Fixtures.DeleteLocalAccount(s_testAccount1.id);
    Fixtures.DeleteLocalAccount(s_testAccount2.id);
    Fixtures.DeleteLocalAccountFile();
  }
}
