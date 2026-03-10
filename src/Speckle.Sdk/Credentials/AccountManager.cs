using System.Net.Http.Headers;
using GraphQL;
using GraphQL.Client.Http;
using Microsoft.Extensions.Logging;
using Speckle.InterfaceGenerator;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Api.GraphQL;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Models.Responses;
using Speckle.Sdk.Common;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Logging;
using Speckle.Sdk.SQLite;

namespace Speckle.Sdk.Credentials;

public partial interface IAccountManager : IDisposable;

/// <summary>
/// Manage accounts locally for desktop applications.
/// </summary>
[GenerateAutoInterface]
public sealed class AccountManager(
  ILogger<AccountManager> logger,
  IGraphQLClientFactory graphQLClientFactory,
  ISpeckleHttp speckleHttp,
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

  /// <summary>
  /// Gets the basic information about a server.
  /// </summary>
  /// <param name="server">Server Information</param>
  /// <returns></returns>
  /// <exception cref="GraphQLHttpRequestException">Request failed on the HTTP layer (received a non-successful response code)</exception>
  /// <exception cref="AggregateException"><inheritdoc cref="GraphQLErrorHandler.EnsureGraphQLSuccess(IGraphQLResponse)"/></exception>
  public async Task<ServerInfo> GetServerInfo(Uri server, CancellationToken cancellationToken = default)
  {
    using var gqlClient = graphQLClientFactory.CreateGraphQLClient(server, null);

    //lang=graphql
    const string QUERY_STRING = "query { serverInfo { name company migration { movedFrom movedTo } } }";

    var request = new GraphQLRequest { Query = QUERY_STRING };

    var response = await gqlClient.SendQueryAsync<ServerInfoResponse>(request, cancellationToken).ConfigureAwait(false);

    response.EnsureGraphQLSuccess();

    ServerInfo serverInfo = response.Data.serverInfo;
    serverInfo.url = server.ToString().TrimEnd('/');

    return response.Data.serverInfo;
  }

  /// <summary>
  /// Gets basic user information given a token and a server.
  /// </summary>
  /// <param name="token"></param>
  /// <param name="server">Server URL</param>
  /// <returns></returns>
  /// <exception cref="GraphQLHttpRequestException">Request failed on the HTTP layer (received a non-successful response code)</exception>
  /// <exception cref="AggregateException"><inheritdoc cref="GraphQLErrorHandler.EnsureGraphQLSuccess(IGraphQLResponse)"/></exception>
  public async Task<UserInfo> GetUserInfo(string token, Uri server, CancellationToken cancellationToken = default)
  {
    using var gqlClient = graphQLClientFactory.CreateGraphQLClient(server, token);

    //language=graphql
    const string QUERY = """
      query { 
        data:activeUser {
          name 
          email 
          id 
          company
        } 
      }
      """;
    var request = new GraphQLRequest { Query = QUERY };

    var response = await gqlClient
      .SendQueryAsync<RequiredResponse<UserInfo>>(request, cancellationToken)
      .ConfigureAwait(false);

    response.EnsureGraphQLSuccess();

    return response.Data.data;
  }

  /// <summary>
  /// The Default Server URL for authentication, can be overridden by placing a file with the alternatrive url in the Speckle folder or with an ENV_VAR
  /// </summary>
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

  /// <param name="id">The Id of the account to fetch</param>
  /// <returns></returns>
  /// <exception cref="SpeckleAccountManagerException">Account with <paramref name="id"/> was not found</exception>
  public Account GetAccount(string id)
  {
    return GetAccounts().FirstOrDefault(acc => acc.id == id)
      ?? throw new SpeckleAccountManagerException($"Account {id} not found");
  }

  /// <summary>
  /// Upgrades an account from the account.serverInfo.movedFrom account to the account.serverInfo.movedTo account
  /// </summary>
  /// <param name="id">Id of the account to upgrade</param>
  public void UpgradeAccount(string id)
  {
    Account account = GetAccount(id);

    if (account.serverInfo.migration?.movedTo is not Uri upgradeUri)
    {
      throw new SpeckleAccountManagerException(
        $"Server with url {account.serverInfo.url} does not have information about the upgraded server"
      );
    }

    account.serverInfo.migration.movedTo = null;
    account.serverInfo.migration.movedFrom = new Uri(account.serverInfo.url);
    account.serverInfo.url = upgradeUri.ToString().TrimEnd('/');

    // setting the id to null will force it to be recreated
    account.id = null!; //TODO this is gross so remove when id is nullable

    RemoveAccount(id);
    _accountStorage.UpdateObject(account.id.NotNull(), JsonConvert.SerializeObject(account));
  }

  public IEnumerable<Account> GetAccounts(string serverUrl)
  {
    return GetAccounts(new Uri(serverUrl));
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
    var localAccounts = GetLocalAccounts();

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

    foreach (var acc in localAccounts)
    {
      yield return acc;
    }
  }

  /// <summary>
  /// Gets the local accounts
  /// These are accounts not handled by Manager and are stored in json format in a local directory
  /// </summary>
  /// <returns></returns>
  private IList<Account> GetLocalAccounts()
  {
    var accountsDir = SpecklePathProvider.AccountsFolderPath;
    if (!Directory.Exists(accountsDir))
    {
      return Array.Empty<Account>();
    }

    var accounts = new List<Account>();
    string[] files = Directory.GetFiles(accountsDir, "*.json", SearchOption.AllDirectories);
    foreach (var file in files)
    {
      try
      {
        var json = File.ReadAllText(file);
        Account? account = JsonConvert.DeserializeObject<Account>(json);

        if (
          account is not null
          && !string.IsNullOrEmpty(account.token)
          && !string.IsNullOrEmpty(account.userInfo.id)
          && !string.IsNullOrEmpty(account.userInfo.email)
          && !string.IsNullOrEmpty(account.userInfo.name)
          && !string.IsNullOrEmpty(account.serverInfo.url)
          && !string.IsNullOrEmpty(account.serverInfo.name)
        )
        {
          accounts.Add(account);
        }
      }
      catch (Exception ex) when (!ex.IsFatal())
      {
        logger.LogWarning(ex, "Failed to load json account at {filePath}", file);
      }
    }

    return accounts;
  }

  /// <summary>
  /// Refetches user and server info for each account
  /// </summary>
  /// <param name="app"> It is defaultAppId in the server. By default it is "sca" to not break existing parts that this function involves.</param>
  /// <returns></returns>
  public async Task UpdateAccounts(CancellationToken ct = default, string app = "sca")
  {
    // need to ToList() the GetAccounts call or the UpdateObject call at the end of this method
    // will not work because sqlite does not support concurrent db calls
    foreach (var account in GetAccounts().ToList())
    {
      try
      {
        Uri url = new(account.serverInfo.url);
        var userServerInfo = await accountFactory.GetUserServerInfo(url, account.token, ct).ConfigureAwait(false);

        //the token has expired
        //TODO: once we get a token expired exception from the server use that instead
        if (userServerInfo.activeUser == null || userServerInfo.serverInfo == null)
        {
          // We were initially was handling refresh token here bc quite a while ago server was returning null
          // for activeUser and serverInfo instead of throwing exception. In short, our logic moved into catch block to cover both.
          throw new SpeckleException("Token is expired");
        }

        account.isOnline = true;
        account.userInfo = userServerInfo.activeUser;
        account.serverInfo = userServerInfo.serverInfo;
      }
      catch (OperationCanceledException)
      {
        throw;
      }
      catch (Exception ex) when (!ex.IsFatal())
      {
        await RefreshAndSetAccountToken(account, app).ConfigureAwait(false);
      }

      ct.ThrowIfCancellationRequested();
      _accountStorage.UpdateObject(account.id, JsonConvert.SerializeObject(account));
    }
  }

  /// <summary>
  /// Mutates the account with new tokens.
  /// </summary>
  /// <param name="account"></param>
  /// <param name="app"></param>
  private async Task RefreshAndSetAccountToken(Account account, string app)
  {
    try
    {
      Uri url = new(account.serverInfo.url);
      var tokenResponse = await GetRefreshedToken(account.refreshToken, url, app).ConfigureAwait(false);
      account.token = tokenResponse.token;
      account.refreshToken = tokenResponse.refreshToken;
      account.isOnline = true;
    }
    catch (Exception ex) when (!ex.IsFatal())
    {
      account.isOnline = false;
    }
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
  /// Retrieves the local identifier for the specified account.
  /// </summary>
  /// <param name="account">The account for which to retrieve the local identifier.</param>
  /// <returns>The local identifier for the specified account in the form of "SERVER_URL?u=USER_ID".</returns>
  /// <remarks>
  /// <inheritdoc cref="Account.GetLocalIdentifier"/>
  /// </remarks>
  [Obsolete(Account.LOCAL_IDENTIFIER_DEPRECATION_MESSAGE)]
  public Uri? GetLocalIdentifierForAccount(Account account)
  {
    var identifier = account.GetLocalIdentifier();

    // Validate account is stored locally
    var searchResult = GetAccountForLocalIdentifier(identifier);

    return searchResult == null ? null : identifier;
  }

  public async Task<UserInfo> Validate(Account account)
  {
    Uri server = new(account.serverInfo.url);
    return await GetUserInfo(account.token, server).ConfigureAwait(false);
  }

  /// <summary>
  /// Gets the account that corresponds to the given local identifier.
  /// </summary>
  /// <param name="localIdentifier">The local identifier of the account.</param>
  /// <returns>The account that matches the local identifier, or null if no match is found.</returns>
  [Obsolete(Account.LOCAL_IDENTIFIER_DEPRECATION_MESSAGE)]
  public Account? GetAccountForLocalIdentifier(Uri localIdentifier)
  {
    var searchResult = GetAccounts()
      .FirstOrDefault(acc =>
      {
        var id = acc.GetLocalIdentifier();
        return id == localIdentifier;
      });

    return searchResult;
  }

  /// <summary>
  /// Adds an account by propting the user to log in via a web flow
  /// </summary>
  /// <param name="serverUrl">Server to use to add the account, if not provied the default Server will be used</param>
  /// <returns></returns>
  public async Task<Account> AddAccount(
    Uri serverUrl,
    string appSecret = "connectorsV3",
    int callbackPort = 29355,
    CancellationToken cancellationToken = default
  )
  {
    //These constants are defined on the server, and specify the scopes the app is requesting
    Uri appCallbackUrl = new($"http://localhost:{callbackPort}");

    logger.LogDebug("Starting to add account for {ServerUrl}", serverUrl);

    TokenExchangeResponse tokenResponse = await authFlow
      .TriggerAuthFlowWithTimeout(serverUrl, appSecret, appCallbackUrl, TimeSpan.FromMinutes(1), cancellationToken)
      .ConfigureAwait(false);

    var account = await accountFactory
      .CreateAccount(serverUrl, tokenResponse.token, tokenResponse.refreshToken, cancellationToken)
      .ConfigureAwait(false);

    _accountStorage.SaveObject(account.id, JsonConvert.SerializeObject(account));

    account.isDefault = !GetAccounts().Any();

    logger.LogInformation("Successfully authenticated account {AccountId} for {ServerUrl}", account.id, serverUrl);
    return account;
  }

  private async Task<TokenExchangeResponse> GetRefreshedToken(string? refreshToken, Uri server, string app)
  {
    try
    {
      using var client = speckleHttp.CreateHttpClient();

      var body = new
      {
        appId = app,
        appSecret = app,
        refreshToken,
      };

      using var content = new StringContent(JsonConvert.SerializeObject(body));
      content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
      var response = await client.PostAsync(new Uri(server, "/auth/token"), content).ConfigureAwait(false);

      return JsonConvert
        .DeserializeObject<TokenExchangeResponse>(await response.Content.ReadAsStringAsync().ConfigureAwait(false))
        .NotNull();
    }
    catch (Exception ex) when (!ex.IsFatal())
    {
      throw new SpeckleException($"Failed to get refreshed token from {server}", ex);
    }
  }
}
