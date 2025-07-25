using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
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
using Stream = System.IO.Stream;

namespace Speckle.Sdk.Credentials;

public partial interface IAccountManager : IDisposable;

/// <summary>
/// Manage accounts locally for desktop applications.
/// </summary>
[GenerateAutoInterface]
public sealed class AccountManager(
  ISpeckleApplication application,
  ILogger<AccountManager> logger,
  IGraphQLClientFactory graphQLClientFactory,
  ISpeckleHttp speckleHttp,
  IAccountFactory accountFactory,
  ISqLiteJsonCacheManagerFactory sqLiteJsonCacheManagerFactory
) : IAccountManager
{
  public const string DEFAULT_SERVER_URL = "https://app.speckle.systems";

  private readonly ISqLiteJsonCacheManager _accountStorage = sqLiteJsonCacheManagerFactory.CreateForUser("Accounts");
  private static volatile bool s_isAddingAccount;
  private readonly ISqLiteJsonCacheManager _accountAddLockStorage = sqLiteJsonCacheManagerFactory.CreateForUser(
    "AccountAddFlow"
  );

  [AutoInterfaceIgnore]
  public void Dispose()
  {
    _accountStorage.Dispose();
    _accountAddLockStorage.Dispose();
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

  private Uri EnsureCorrectServerUrl(Uri? server)
  {
    var localUrl = server;
    if (localUrl == null)
    {
      localUrl = GetDefaultServerUrl();
      logger.LogDebug("The provided server url was null or empty. Changed to the default url {serverUrl}", localUrl);
    }
    return localUrl;
  }

  private void EnsureGetAccessCodeFlowIsSupported()
  {
    if (!HttpListener.IsSupported)
    {
      logger.LogError("HttpListener not supported");
      throw new PlatformNotSupportedException("Your operating system is not supported");
    }
  }

  private async Task<string> GetAccessCode(Uri server, string challenge, TimeSpan timeout)
  {
    EnsureGetAccessCodeFlowIsSupported();

    logger.LogDebug("Starting auth process for {server}/authn/verify/sca/{challenge}", server, challenge);

    var accessCode = "";

    Process.Start(new ProcessStartInfo($"{server}/authn/verify/sca/{challenge}") { UseShellExecute = true });

    var task = Task.Run(() =>
    {
      using var listener = new HttpListener();
      var localUrl = "http://localhost:29363/";
      listener.Prefixes.Add(localUrl);
      listener.Start();
      logger.LogDebug("Listening for auth redirects on {localUrl}", localUrl);
      // Note: The GetContext method blocks while waiting for a request.
      HttpListenerContext context = listener.GetContext();
      HttpListenerRequest request = context.Request;
      HttpListenerResponse response = context.Response;

      accessCode = request.QueryString["access_code"];
      logger.LogDebug("Got access code {accessCode}", accessCode);

      string message =
        accessCode != null
          ? "Success!<br/><br/>You can close this window now.<script>window.close();</script>"
          : "Oups, something went wrong...!";

      var responseString =
        $"<HTML><BODY Style='background: linear-gradient(to top right, #ffffff, #c8e8ff); font-family: Roboto, sans-serif; font-size: 2rem; font-weight: 500; text-align: center;'><br/>{message}</BODY></HTML>";
      byte[] buffer = Encoding.UTF8.GetBytes(responseString);
      response.ContentLength64 = buffer.Length;
      Stream output = response.OutputStream;
      output.Write(buffer, 0, buffer.Length);
      output.Close();
      logger.LogDebug("Processed finished processing the access code");
      listener.Stop();
      listener.Close();
    });

    var completedTask = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);

    // this is means the task timed out
    if (completedTask != task)
    {
      logger.LogWarning(
        "Local auth flow failed to complete within the timeout window. Access code is {accessCode}",
        accessCode
      );
      throw new AuthFlowException("Local auth flow failed to complete within the timeout window");
    }

    if (task.IsFaulted && task.Exception is not null)
    {
      logger.LogError(
        task.Exception,
        "Getting access code flow failed with {exceptionMessage}",
        task.Exception.Message
      );
      throw new AuthFlowException($"Auth flow failed: {task.Exception.Message}", task.Exception);
    }

    // task completed within timeout
    logger.LogInformation(
      "Local auth flow completed successfully within the timeout window. Access code is {accessCode}",
      accessCode
    );
    return accessCode;
  }

  private async Task<Account> CreateAccount(string accessCode, string challenge, Uri server)
  {
    try
    {
      var tokenResponse = await GetToken(accessCode, challenge, server).ConfigureAwait(false);

      var account = await accountFactory
        .CreateAccount(server, tokenResponse.token, tokenResponse.refreshToken)
        .ConfigureAwait(false);

      account.isDefault = !GetAccounts().Any();

      logger.LogInformation("Successfully created account for {serverUrl}", server);

      return account;
    }
    catch (Exception ex) when (!ex.IsFatal())
    {
      throw new SpeckleAccountManagerException("Failed to create account from access code and challenge", ex);
    }
  }

  private void TryLockAccountAddFlow(TimeSpan timespan)
  {
    // use a static variable to quickly
    // prevent launching this flow multiple times
    if (s_isAddingAccount)
    {
      // this should probably throw with an error message
      throw new SpeckleAccountFlowLockedException("The account add flow is already launched.");
    }

    // this uses the SQLite transport to store locks
    var lockIds = _accountAddLockStorage.GetAllObjects().Select(x => x.Id).OrderByDescending(d => d).ToList();
    var now = DateTime.Now;
    foreach (var l in lockIds)
    {
      var lockArray = l.Split('@');
      var lockName = lockArray.Length == 2 ? lockArray[0] : "the other app";
      var lockTime =
        lockArray.Length == 2
          ? DateTime.ParseExact(lockArray[1], "o", null)
          : DateTime.ParseExact(lockArray[0], "o", null);

      if (lockTime > now)
      {
        var lockString = string.Format("{0:mm} minutes {0:ss} seconds", lockTime - now);
        throw new SpeckleAccountFlowLockedException(
          $"The account add flow was already started in {lockName}, retry in {lockString}"
        );
      }
    }

    var lockId = application.ApplicationAndVersion + "@" + DateTime.Now.Add(timespan).ToString("o");

    // using the lock release time as an id and value
    // for ease of deletion and retrieval
    _accountAddLockStorage.SaveObject(lockId, lockId);
    s_isAddingAccount = true;
  }

  private void UnlockAccountAddFlow()
  {
    s_isAddingAccount = false;
    // make sure all old locks are removed
    foreach (var (id, _) in _accountAddLockStorage.GetAllObjects())
    {
      _accountAddLockStorage.DeleteObject(id);
    }
  }

  /// <summary>
  /// Adds an account by propting the user to log in via a web flow
  /// </summary>
  /// <param name="server">Server to use to add the account, if not provied the default Server will be used</param>
  /// <returns></returns>
  public async Task AddAccount(Uri? server = null)
  {
    logger.LogDebug("Starting to add account for {serverUrl}", server);

    server = EnsureCorrectServerUrl(server);

    // locking for 1 minute
    var timeout = TimeSpan.FromMinutes(1);
    // this is not part of the try finally block
    // we do not want to clean up the existing locks
    TryLockAccountAddFlow(timeout);
    var challenge = GenerateChallenge();

    try
    {
      string accessCode = await GetAccessCode(server, challenge, timeout).ConfigureAwait(false);
      if (string.IsNullOrEmpty(accessCode))
      {
        throw new SpeckleAccountManagerException("Access code is invalid");
      }

      var account = await CreateAccount(accessCode, challenge, server).ConfigureAwait(false);

      //if the account already exists it will not be added again
      _accountStorage.SaveObject(account.id, JsonConvert.SerializeObject(account));
      logger.LogDebug("Finished adding account {accountId} for {serverUrl}", account.id, server);
    }
    catch (SpeckleAccountManagerException ex)
    {
      logger.LogCritical(ex, "Failed to add account: {exceptionMessage}", ex.Message);
      // rethrowing any known errors
      throw;
    }
    catch (Exception ex) when (!ex.IsFatal())
    {
      logger.LogCritical(ex, "Failed to add account: {exceptionMessage}", ex.Message);
      throw new SpeckleAccountManagerException($"Failed to add account: {ex.Message}", ex);
    }
    finally
    {
      UnlockAccountAddFlow();
    }
  }

  private async Task<TokenExchangeResponse> GetToken(string accessCode, string challenge, Uri server)
  {
    try
    {
      using var client = speckleHttp.CreateHttpClient();

      var body = new
      {
        appId = "sca",
        appSecret = "sca",
        accessCode,
        challenge,
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
      throw new SpeckleException($"Failed to get authentication token from {server}", ex);
    }
  }

  private async Task<TokenExchangeResponse> GetRefreshedToken(string? refreshToken, Uri server, string app = "sca")
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

  private static string GenerateChallenge()
  {
#if NET8_0
    byte[] challengeData = RandomNumberGenerator.GetBytes(32);
#else
    using RNGCryptoServiceProvider rng = new();
    byte[] challengeData = new byte[32];
    rng.GetBytes(challengeData);
#endif
    //escaped chars like % do not play nice with the server
    return Regex.Replace(Convert.ToBase64String(challengeData), @"[^\w\.@-]", "");
  }
}
