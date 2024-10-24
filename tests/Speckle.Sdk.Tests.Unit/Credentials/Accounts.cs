using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Host;

namespace Speckle.Sdk.Tests.Unit.Credentials;

[TestFixture]
public class CredentialInfrastructure
{
  private IAccountManager _accountManager;

  [OneTimeSetUp]
  public static void SetUp()
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

  [SetUp]
  public void Setup2()
  {
    var serviceProvider = TestServiceSetup.GetServiceProvider();
    _accountManager = serviceProvider.GetRequiredService<IAccountManager>();
  }

  [OneTimeTearDown]
  public static void TearDown()
  {
    Fixtures.DeleteLocalAccount(s_testAccount1.id);
    Fixtures.DeleteLocalAccount(s_testAccount2.id);
    Fixtures.DeleteLocalAccountFile();
  }

  private static Account s_testAccount1,
    s_testAccount2,
    s_testAccount3;

  [Test]
  public void GetAllAccounts()
  {
    var accs = _accountManager.GetAccounts().ToList();
    Assert.That(accs, Has.Count.GreaterThanOrEqualTo(3)); // Tests are adding three accounts, you might have extra accounts on your machine when testing :D
  }

  [Test]
  public void GetAccount_ById()
  {
    var result = _accountManager.GetAccount(s_testAccount1.id);

    Assert.That(result, Is.EqualTo(s_testAccount1));
  }

  [Test]
  public void GetAccount_ById_ThrowsWhenNotFound()
  {
    Assert.Throws<SpeckleAccountManagerException>(() => _accountManager.GetAccount("Non_existent_id"));
  }

  public static IEnumerable<Account> TestCases()
  {
    SetUp();
    return new[] { s_testAccount1, s_testAccount2, s_testAccount3 };
  }

  [Test]
  [TestCaseSource(nameof(TestCases))]
  public void GetAccountsForServer(Account target)
  {
    var accs = _accountManager.GetAccounts(target.serverInfo.url).ToList();

    Assert.That(accs, Has.Count.EqualTo(1));

    var acc = accs[0];

    Assert.That(acc, Is.Not.SameAs(target), "We expect new objects (no reference equality)");
    Assert.That(acc.serverInfo.company, Is.EqualTo(target.serverInfo.company));
    Assert.That(acc.serverInfo.url, Is.EqualTo(target.serverInfo.url));
    Assert.That(acc.refreshToken, Is.EqualTo(target.refreshToken));
    Assert.That(acc.token, Is.EqualTo(target.token));
  }

  [Test]
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

    Assert.That(acc1, Is.Not.EqualTo(acc2));
  }
}
