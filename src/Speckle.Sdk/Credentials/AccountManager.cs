using GraphQL.Client.Http;
using Microsoft.Extensions.Logging;
using Speckle.InterfaceGenerator;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Common;
using Speckle.Sdk.Logging;
using Speckle.Sdk.SQLite;

namespace Speckle.Sdk.Credentials;

public partial interface IAccountManager : IDisposable;

/// <summary>
/// Manages <see cref="Account"/> data in the local sqlite account store
/// </summary>
[GenerateAutoInterface]
public sealed class AccountManager(
  ILogger<AccountManager> logger,
  IAccountFactory accountFactory,
  IAuthFlow authFlow,
  ISqLiteJsonCacheManagerFactory sqLiteJsonCacheManagerFactory
) : IAccountManager
{
  public const string DEFAULT_SERVER_URL = "https://app.speckle.systems";

  private readonly ISqLiteJsonCacheManager _accountStorage = sqLiteJsonCacheManagerFactory.CreateForUser("Accounts");

  [AutoInterfaceIgnore]
  public void Dispose()
  {
    _accountStorage.Dispose();
  }

  /// <param name="id">The Id of the account to fetch</param>
  /// <returns></returns>
  /// <exception cref="SpeckleAccountManagerException">Account with <paramref name="id"/> was not found</exception>
  public Account GetAccount(string id)
  {
    return GetAccounts().FirstOrDefault(acc => acc.id == id)
      ?? throw new SpeckleAccountManagerException($"Account {id} not found");
  }

  /// <summary>
  /// Returns all unique accounts matching the serverUrl provided. If an account exists on more than one server,
  /// typically because it has been migrated, then only the upgraded account (and therefore server) are returned.
  /// Accounts are deemed to be the same when the Account.Id matches.
  /// </summary>
  /// <param name="serverUrl">Uri for server.</param>
  public IEnumerable<Account> GetAccounts(Uri serverUrl)
  {
    var accounts = GetAccounts().ToList();
    List<Account> filtered = new();

    foreach (var acc in accounts)
    {
      if (acc.serverInfo?.migration?.movedFrom == serverUrl)
      {
        filtered.Add(acc);
      }
    }

    foreach (var acc in accounts)
    {
      // we use the userInfo to detect the same account rather than the account.id
      // which should NOT match for essentially the same accounts but on different servers - i.e. FE1 & FE2
      if (new Uri(acc.serverInfo.url) == serverUrl && !filtered.Any(x => x.userInfo.id == acc.userInfo.id))
      {
        filtered.Add(acc);
      }
    }

    return filtered;
  }

  /// <summary>
  /// Gets this environment's default account if any. If there is no default, the first found will be returned and set as default.
  /// </summary>
  /// <returns>The default account or null.</returns>
  public Account? GetDefaultAccount()
  {
    var defaultAccount = GetAccounts().FirstOrDefault(acc => acc.isDefault);
    if (defaultAccount != null)
    {
      return defaultAccount;
    }

    var firstAccount = GetAccounts().FirstOrDefault();
    if (firstAccount == null)
    {
      logger.LogInformation("No Speckle accounts found. Visit the Speckle web app to create one");
    }

    return firstAccount;
  }

  /// <summary>
  /// Gets all the accounts present in this environment.
  /// </summary>
  /// <remarks>This function does have potential side effects. Any invalid accounts found while enumerating will be removed</remarks>
  /// <returns>Un-enumerated enumerable of accounts</returns>
  public IEnumerable<Account> GetAccounts()
  {
    static bool IsInvalid(Account ac) => ac.userInfo == null || ac.serverInfo == null;

    var sqlAccounts = _accountStorage.GetAllObjects().Select(x => JsonConvert.DeserializeObject<Account>(x.Json));

    foreach (var acc in sqlAccounts)
    {
      acc.NotNull();
      if (IsInvalid(acc))
      {
        RemoveAccount(acc.id);
      }
      else
      {
        yield return acc;
      }
    }
  }

  /// <summary>
  /// Refetches all local accounts (in local db), including <see cref="ServerInfo"/> and <see cref="UserInfo"/>.
  /// If the <see cref="Account.token"/> looks to be expired, this function will also attempt to use the <see cref="Account.refreshToken"/> to refresh it.
  /// Will write the changes to the local accounts db
  /// </summary>
  /// <seealso cref="UpdateAccount"/>
  /// <param name="cancellationToken"></param>
  /// <exception cref="AggregateException"></exception>
  public async Task UpdateAccount(Account account, CancellationToken cancellationToken = default)
  {
    string oldAccountId = account.id;
    await UpdateAccountInMemory(account, cancellationToken).ConfigureAwait(false);

    if (oldAccountId != account.id)
    {
      // ID may have changed, e.g. users email changed, or server url migrated
      _accountStorage.DeleteObject(oldAccountId);
    }
    _accountStorage.UpdateObject(account.id, JsonConvert.SerializeObject(account));
  }

  /// <summary>
  /// Refetches the <paramref name="account"/> information, including <see cref="ServerInfo"/> and <see cref="UserInfo"/>
  ///
  /// Will only mutate <paramref name="account"/> in memory only, and only if successful.
  /// </summary>
  /// <seealso cref="UpdateAccount"/>
  /// <param name="account"></param>
  /// <param name="cancellationToken"></param>
  /// <exception cref="GraphQLHttpRequestException"></exception>
  public async Task UpdateAccountInMemory(Account account, CancellationToken cancellationToken = default)
  {
    Uri url = account.serverInfo.migration?.movedTo ?? new(account.serverInfo.url);

    ActiveUserServerInfoResponse userServerInfo = await accountFactory
      .GetUserServerInfo(url, account.token, cancellationToken)
      .ConfigureAwait(false);

    if (userServerInfo.activeUser == null)
    {
      throw new SpeckleException("GraphQL response indicated that the ActiveUser could not be found");
    }
    account.userInfo = userServerInfo.activeUser;
    account.serverInfo = userServerInfo.serverInfo;
    //This is a bit gross, since id is not marked nullable
    //but this will force re-generate the id (e.g. if the user's email, or  servers url has changed)
    account.id = null!;
  }

  /// <summary>
  /// Removes an account
  /// </summary>
  /// <param name="id">ID of the account to remove</param>
  public void RemoveAccount(string id)
  {
    //TODO: reset default account
    _accountStorage.DeleteObject(id);

    var accounts = GetAccounts().ToArray();

    if (accounts.Length != 0 && !accounts.Any(x => x.isDefault))
    {
      ChangeDefaultAccount(accounts.First().id);
    }
  }

  /// <summary>
  /// Changes the default account
  /// </summary>
  /// <param name="id"></param>
  public void ChangeDefaultAccount(string id)
  {
    foreach (var account in GetAccounts())
    {
      if (account.id != id)
      {
        account.isDefault = false;
      }
      else
      {
        account.isDefault = true;
      }
      _accountStorage.UpdateObject(account.id, JsonConvert.SerializeObject(account));
    }
  }

  /// <summary>
  /// Adds an account to local storage by prompting the user to log in via their browser.
  /// </summary>
  /// <example>
  /// <code>
  /// Account account = await AuthenticateAccount(new Uri("https://app.speckle.systems"), TimeSpan.FromMinutes(1));
  /// </code>
  /// </example>
  /// <param name="serverUrl"></param>
  /// <param name="timeout">Timeout for user to auth with browser, recommend 1 min timeout</param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  public async Task<Account> AuthenticateAccount(Uri serverUrl, TimeSpan timeout, CancellationToken cancellationToken)
  {
    logger.LogDebug("Starting to add account for {ServerUrl}", serverUrl);

    TokenExchangeResponse tokenResponse = await authFlow
      .TriggerAuthFlowWithTimeout(serverUrl, AuthApp.ConnectorsV3, timeout, cancellationToken)
      .ConfigureAwait(false);

    return await CreateAndAddAccount(serverUrl, tokenResponse, cancellationToken).ConfigureAwait(false);
  }

  public async Task<Account> CreateAndAddAccount(
    Uri serverUrl,
    TokenExchangeResponse tokenResponse,
    CancellationToken cancellationToken
  )
  {
    var account = await accountFactory
      .CreateAccount(serverUrl, tokenResponse.token, tokenResponse.refreshToken, cancellationToken)
      .ConfigureAwait(false);
    account.isDefault = !GetAccounts().Any();

    _accountStorage.SaveObject(account.id, JsonConvert.SerializeObject(account));
    logger.LogInformation("Successfully authenticated account {AccountId} for {ServerUrl}", account.id, serverUrl);
    return account;
  }

  /// <summary>
  /// The Default Server URL for authentication, can be overridden by placing a file with the alternative url in the Speckle folder or with an ENV_VAR
  /// </summary>
  [Obsolete("Unused")]
  public Uri GetDefaultServerUrl()
  {
    var customServerUrl = "";

    // first mechanism, check for local file
    var customServerFile = Path.Combine(SpecklePathProvider.UserSpeckleFolderPath, "server");
    if (File.Exists(customServerFile))
    {
      customServerUrl = File.ReadAllText(customServerFile);
    }

    // second mechanism, check ENV VAR
    var customServerEnvVar = Environment.GetEnvironmentVariable("SPECKLE_SERVER");
    if (!string.IsNullOrEmpty(customServerEnvVar))
    {
      customServerUrl = customServerEnvVar;
    }

    if (!string.IsNullOrEmpty(customServerUrl))
    {
      if (Uri.TryCreate(customServerUrl, UriKind.Absolute, out Uri? url))
      {
        return url;
      }
    }

    return new Uri(DEFAULT_SERVER_URL);
  }

  [Obsolete("Use Uri overload")]
  public IEnumerable<Account> GetAccounts(string serverUrl)
  {
    return GetAccounts(new Uri(serverUrl));
  }

  [Obsolete("Use UpdateAccount instead for more control over error handling", true)]
  public Task UpdateAccounts(CancellationToken ct = default, string app = "sca") => throw new NotImplementedException();

  [Obsolete("Use UpdateAccount instead", true)]
  public void UpgradeAccount(string id) => throw new NotImplementedException();

  [Obsolete($"Use {nameof(AuthenticateAccount)} instead", true)]
  public Task AddAccount(Uri? server = null) => throw new NotImplementedException();

  [Obsolete("Use serverInfo stored on a client instead", true)]
  public Task<ServerInfo> GetServerInfo(Uri server, CancellationToken cancellationToken = default) =>
    throw new NotImplementedException();

  [Obsolete("Use userInfo stored on a client instead", true)]
  public Task<UserInfo> GetUserInfo(string token, Uri server, CancellationToken cancellationToken = default) =>
    throw new NotImplementedException();

  [Obsolete("Accounts must now be stored in sqlite db, no more json workaround", true)]
  public IList<Account> GetLocalAccounts() => throw new NotImplementedException();

  [Obsolete("Use UpdateAccount or UpdateAccountInMemory Instead", true)]
  public IList<Account> Validate() => throw new NotImplementedException();
}
