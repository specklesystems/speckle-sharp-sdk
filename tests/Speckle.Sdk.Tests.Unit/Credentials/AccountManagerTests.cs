using Microsoft.Extensions.Logging;
using Moq;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.SQLite;
using Speckle.Sdk.Testing;

namespace Speckle.Sdk.Tests.Unit.Credentials;

public sealed class AccountManagerTests : MoqTest
{
  private class TestAccountFactory : IAccountFactory
  {
    public Task<Account> CreateAccount(
      Uri serverUrl,
      string speckleToken,
      string? refreshToken = default,
      CancellationToken cancellationToken = default
    ) => throw new NotImplementedException();

    public Task<ActiveUserServerInfoResponse> GetUserServerInfo(
      Uri serverUrl,
      string? authToken,
      CancellationToken ct
    ) => throw new NotImplementedException();
  }

  private readonly Mock<ISpeckleApplication> _mockApplication;
  private readonly Mock<ILogger<AccountManager>> _mockLogger;
  private readonly Mock<IGraphQLClientFactory> _mockGraphQLClientFactory;
  private readonly Mock<ISpeckleHttp> _mockSpeckleHttp;
  private readonly IAccountFactory _mockAccountFactory;
  private readonly Mock<ISqLiteJsonCacheManagerFactory> _mockSqLiteJsonCacheManagerFactory;
  private readonly Mock<ISqLiteJsonCacheManager> _mockAccountStorage;
  private readonly Mock<ISqLiteJsonCacheManager> _mockAccountAddLockStorage;

#pragma warning disable CA2213
  private readonly AccountManager _accountManager;
#pragma warning restore CA2213

  public AccountManagerTests()
  {
    _mockApplication = Create<ISpeckleApplication>();
    _mockLogger = Create<ILogger<AccountManager>>(MockBehavior.Loose);
    _mockGraphQLClientFactory = Create<IGraphQLClientFactory>();
    _mockSpeckleHttp = Create<ISpeckleHttp>();
    _mockAccountFactory = new TestAccountFactory();
    _mockSqLiteJsonCacheManagerFactory = Create<ISqLiteJsonCacheManagerFactory>();

    _mockAccountStorage = Create<ISqLiteJsonCacheManager>();
    _mockAccountAddLockStorage = Create<ISqLiteJsonCacheManager>();

    _mockSqLiteJsonCacheManagerFactory.Setup(f => f.CreateForUser("Accounts")).Returns(_mockAccountStorage.Object);
    _mockSqLiteJsonCacheManagerFactory
      .Setup(f => f.CreateForUser("AccountAddFlow"))
      .Returns(_mockAccountAddLockStorage.Object);

    _accountManager = new AccountManager(
      _mockApplication.Object,
      _mockLogger.Object,
      _mockGraphQLClientFactory.Object,
      _mockSpeckleHttp.Object,
      _mockAccountFactory,
      _mockSqLiteJsonCacheManagerFactory.Object
    );
  }

  [Fact]
  public void GetDefaultServerUrl_ReturnsDefaultUrl_WhenNoCustomUrlProvided()
  {
    // Act
    var result = _accountManager.GetDefaultServerUrl();

    // Assert
    Assert.Equal(new Uri(AccountManager.DEFAULT_SERVER_URL), result);
  }

  [Fact]
  public void GetAccount_ReturnsAccount_WhenExists()
  {
    // Arrange
    var accountId = "test-account-id";
    var account = CreateTestAccount(accountId);

    _mockAccountStorage
      .Setup(s => s.GetAllObjects())
      .Returns(new[] { (accountId, JsonConvert.SerializeObject(account)) });

    // Act
    var result = _accountManager.GetAccount(accountId);

    // Assert
    Assert.Equal(accountId, result.id);
    Assert.Equal(account.userInfo.name, result.userInfo.name);
  }

  [Fact]
  public void GetAccount_ThrowsException_WhenNotExists()
  {
    // Arrange
    var accountId = "non-existent-id";

    _mockAccountStorage.Setup(s => s.GetAllObjects()).Returns([]);

    // Act & Assert
    var exception = Assert.Throws<SpeckleAccountManagerException>(() => _accountManager.GetAccount(accountId));
    Assert.Equal($"Account {accountId} not found", exception.Message);
  }

  [Fact]
  public void GetAccounts_StringParameter_CallsUriOverload()
  {
    // Arrange
    var serverUrl = "https://test.speckle.systems";
    var account = CreateTestAccount("test-account-id");
    account.serverInfo.url = serverUrl;

    _mockAccountStorage
      .Setup(s => s.GetAllObjects())
      .Returns(new[] { (account.id, JsonConvert.SerializeObject(account)) });

    // Act
    var result = _accountManager.GetAccounts(serverUrl).ToList();

    // Assert
    Assert.Single(result);
    Assert.Equal(serverUrl, result[0].serverInfo.url);
  }

  [Fact]
  public void GetAccounts_UriParameter_ReturnsMatchingAccounts()
  {
    // Arrange
    var serverUri = new Uri("https://test.speckle.systems");
    var account = CreateTestAccount("test-account-id");
    account.serverInfo.url = serverUri.ToString();

    _mockAccountStorage
      .Setup(s => s.GetAllObjects())
      .Returns(new[] { (account.id, JsonConvert.SerializeObject(account)) });

    // Act
    var result = _accountManager.GetAccounts(serverUri).ToList();

    // Assert
    Assert.Single(result);
    Assert.Equal(serverUri.ToString(), result[0].serverInfo.url);
  }

  [Fact]
  public void GetDefaultAccount_ReturnsMarkedDefaultAccount_WhenExists()
  {
    // Arrange
    var defaultAccount = CreateTestAccount("default-account");
    defaultAccount.isDefault = true;

    var regularAccount = CreateTestAccount("regular-account");

    _mockAccountStorage
      .Setup(s => s.GetAllObjects())
      .Returns(
        new[]
        {
          (defaultAccount.id, JsonConvert.SerializeObject(defaultAccount)),
          (regularAccount.id, JsonConvert.SerializeObject(regularAccount)),
        }
      );

    // Act
    var result = _accountManager.GetDefaultAccount();

    // Assert
    Assert.NotNull(result);
    Assert.Equal("default-account", result!.id);
    Assert.True(result.isDefault);
  }

  [Fact]
  public void GetDefaultAccount_ReturnsFirstAccount_WhenNoDefaultExists()
  {
    // Arrange
    var account1 = CreateTestAccount("account-1");
    var account2 = CreateTestAccount("account-2");

    _mockAccountStorage
      .Setup(s => s.GetAllObjects())
      .Returns(
        new[]
        {
          (account1.id, JsonConvert.SerializeObject(account1)),
          (account2.id, JsonConvert.SerializeObject(account2)),
        }
      );

    // Act
    var result = _accountManager.GetDefaultAccount();

    // Assert
    Assert.NotNull(result);
    Assert.Equal("account-1", result!.id);
  }

  [Fact]
  public void GetDefaultAccount_ReturnsNull_WhenNoAccounts()
  {
    // Arrange
    _mockAccountStorage.Setup(s => s.GetAllObjects()).Returns([]);

    // Act
    var result = _accountManager.GetDefaultAccount();

    // Assert
    Assert.Null(result);
  }

  [Fact]
  public void GetAccounts_SkipsInvalidAccounts()
  {
    // Arrange
    var validAccount = CreateTestAccount("valid-account");
    validAccount.isDefault = true;

    var invalidAccount = new Account
    {
      token = null,
      id = "invalid-account",
      serverInfo = null!,
      userInfo = null!,
    }; // intentional use of null-forgiving operator since we're testing json account validation logic

    var deleteCalled = false;

    _mockAccountStorage
      .Setup(s => s.GetAllObjects())
      .Returns(() =>
      {
        if (deleteCalled)
        {
          return [(validAccount.id, JsonConvert.SerializeObject(validAccount))];
        }
        return
        [
          (validAccount.id, JsonConvert.SerializeObject(validAccount)),
          (invalidAccount.id, JsonConvert.SerializeObject(invalidAccount)),
        ];
      });

    _mockAccountStorage.Setup(s => s.DeleteObject(invalidAccount.id)).Callback(() => deleteCalled = true);
    // Act
    var result = _accountManager.GetAccounts().ToList();

    // Assert
    Assert.Single(result);
    Assert.Equal("valid-account", result[0].id);
    _mockAccountStorage.Verify(s => s.DeleteObject(invalidAccount.id), Times.Once);
  }

  [Fact]
  public void RemoveAccount_RemovesAccount()
  {
    // Arrange
    var accountId = "account-to-remove";

    _mockAccountStorage.Setup(s => s.DeleteObject(accountId));
    _mockAccountStorage.Setup(s => s.GetAllObjects()).Returns([]);

    // Act
    _accountManager.RemoveAccount(accountId);

    // Assert
    _mockAccountStorage.Verify(s => s.DeleteObject(accountId), Times.Once);
  }

  [Fact]
  public void RemoveAccount_SetsNewDefaultAccount_WhenDefaultRemoved()
  {
    // Arrange
    var defaultAccountId = "default-account";
    var regularAccountId = "regular-account";

    var regularAccount = CreateTestAccount(regularAccountId);

    _mockAccountStorage.Setup(s => s.DeleteObject(defaultAccountId));
    _mockAccountStorage
      .Setup(s => s.GetAllObjects())
      .Returns(new[] { (regularAccountId, JsonConvert.SerializeObject(regularAccount)) });
    _mockAccountStorage.Setup(s => s.UpdateObject(regularAccountId, It.IsAny<string>()));

    // Act
    _accountManager.RemoveAccount(defaultAccountId);

    // Assert
    _mockAccountStorage.Verify(s => s.DeleteObject(defaultAccountId), Times.Once);
    _mockAccountStorage.Verify(
      s => s.UpdateObject(regularAccountId, It.Is<string>(json => json.Contains("\"isDefault\":true"))),
      Times.Once
    );
  }

  [Fact]
  public void ChangeDefaultAccount_UpdatesDefaultAccount()
  {
    // Arrange
    var account1 = CreateTestAccount("account-1");
    account1.isDefault = true;

    var account2 = CreateTestAccount("account-2");

    _mockAccountStorage
      .Setup(s => s.GetAllObjects())
      .Returns(
        new[]
        {
          (account1.id, JsonConvert.SerializeObject(account1)),
          (account2.id, JsonConvert.SerializeObject(account2)),
        }
      );

    _mockAccountStorage.Setup(s => s.UpdateObject(account1.id, It.IsAny<string>()));
    _mockAccountStorage.Setup(s => s.UpdateObject(account2.id, It.IsAny<string>()));

    // Act
    _accountManager.ChangeDefaultAccount(account2.id);

    // Assert
    _mockAccountStorage.Verify(
      s => s.UpdateObject(account1.id, It.Is<string>(json => json.Contains("\"isDefault\":false"))),
      Times.Once
    );
    _mockAccountStorage.Verify(
      s => s.UpdateObject(account2.id, It.Is<string>(json => json.Contains("\"isDefault\":true"))),
      Times.Once
    );
  }

  [Fact]
  public void GetLocalIdentifierForAccount_ReturnsIdentifier_WhenAccountExists()
  {
    // Arrange
    var account = CreateTestAccount("test-account");
    var expectedUri = new Uri($"{account.serverInfo.url}?id={account.userInfo.id}");

    _mockAccountStorage.Setup(s => s.GetAllObjects()).Returns(new[] { ("bad", JsonConvert.SerializeObject(account)) });

    // Act
    var result = _accountManager.GetLocalIdentifierForAccount(account);

    // Assert
    Assert.NotNull(result);
    Assert.Equal(expectedUri, result);
  }

  [Fact]
  public void GetLocalIdentifierForAccount_ReturnsNull_WhenAccountDoesNotExist()
  {
    // Arrange
    var account = CreateTestAccount("non-existent-account");

    _mockAccountStorage.Setup(s => s.GetAllObjects()).Returns([]);

    // Act
    var result = _accountManager.GetLocalIdentifierForAccount(account);

    // Assert
    Assert.Null(result);
  }

  [Fact]
  public void GetAccountForLocalIdentifier_ReturnsAccount_WhenMatches()
  {
    // Arrange
    var account = CreateTestAccount("test-account");
    var localIdentifier = new Uri($"{account.serverInfo.url}?id={account.userInfo.id}");

    _mockAccountStorage.Setup(s => s.GetAllObjects()).Returns(new[] { ("bad", JsonConvert.SerializeObject(account)) });

    // Act
    var result = _accountManager.GetAccountForLocalIdentifier(localIdentifier);

    // Assert
    Assert.NotNull(result);
    Assert.Equal(account.id, result!.id);
  }

  [Fact]
  public void GetAccountForLocalIdentifier_ReturnsNull_WhenNoMatch()
  {
    // Arrange
    var account = CreateTestAccount("test-account");
    var localIdentifier = new Uri("https://different.url?u=different-user");

    _mockAccountStorage.Setup(s => s.GetAllObjects()).Returns(new[] { ("bad", JsonConvert.SerializeObject(account)) });

    // Act
    var result = _accountManager.GetAccountForLocalIdentifier(localIdentifier);

    // Assert
    Assert.Null(result);
  }

  // Helper method to create a test account
  private static Account CreateTestAccount(string id)
  {
    return new Account
    {
      id = id,
      token = "test-token",
      refreshToken = "refresh-token",
      isDefault = false,
      isOnline = true,
      userInfo = new UserInfo
      {
        id = "user-id",
        name = "Test User",
        email = "test@example.com",
        company = "Test Company",
      },
      serverInfo = new ServerInfo
      {
        name = "Test Server",
        url = "https://test.speckle.systems",
        company = "Speckle",
      },
    };
  }
}
