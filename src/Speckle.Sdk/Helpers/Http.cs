using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Extensions.Http;
using Polly.Timeout;
using Speckle.Sdk.Common;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Logging;

namespace Speckle.Sdk.Helpers;

public static class Http
{
  public const int DEFAULT_TIMEOUT_SECONDS = 60;

  public static IEnumerable<TimeSpan> DefaultDelay()
  {
    return Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromMilliseconds(200), 5);
  }

  public static IAsyncPolicy<HttpResponseMessage> HttpAsyncPolicy(
    IEnumerable<TimeSpan>? delay = null,
    int timeoutSeconds = DEFAULT_TIMEOUT_SECONDS
  )
  {
    var retryPolicy = HttpPolicyExtensions
      .HandleTransientHttpError()
      .Or<TimeoutRejectedException>()
      .WaitAndRetryAsync(
        delay ?? DefaultDelay(),
        (ex, timeSpan, retryAttempt, context) =>
        {
          context.Remove("retryCount");
          context.Add("retryCount", retryAttempt);
        }
      );

    var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(timeoutSeconds);

    return Policy.WrapAsync(retryPolicy, timeoutPolicy);
  }

  /// <summary>
  /// Checks if the user has a valid internet connection by first pinging cloudfare (fast)
  /// and then trying get from the default Speckle server (slower)
  /// </summary>
  /// <returns>True if the user is connected to the internet, false otherwise.</returns>
  public static async Task<bool> UserHasInternet()
  {
    Uri? defaultServer = null;
    try
    {
      //Perform a quick ping test e.g. to cloudflaire dns, as is quicker than pinging server
      if (await Ping("1.1.1.1").ConfigureAwait(false))
      {
        return true;
      }

      defaultServer = AccountManager.GetDefaultServerUrl();
      await HttpPing(defaultServer).ConfigureAwait(false);
      return true;
    }
    catch (HttpRequestException ex)
    {
      using var activity = SpeckleActivityFactory.Start();
      activity?.SetTag("defaultServer", defaultServer);
      SpeckleLog.Logger.Warning(ex, "Failed to ping internet");

      return false;
    }
  }

  /// <summary>
  /// Pings a specific url to verify it's accessible. Retries 3 times.
  /// </summary>
  /// <param name="hostnameOrAddress">The hostname or address to ping.</param>
  /// <returns>True if the the status code is 200, false otherwise.</returns>
  public static async Task<bool> Ping(string hostnameOrAddress)
  {
    SpeckleLog.Logger.Information("Pinging {hostnameOrAddress}", hostnameOrAddress);
    var policy = Policy
      .Handle<PingException>()
      .Or<SocketException>()
      .WaitAndRetryAsync(
        DefaultDelay(),
        (ex, timeSpan, retryAttempt, context) => {
          //Log.Information(
          //  ex,
          //  "The http request failed with {exceptionType} exception retrying after {cooldown} milliseconds. This is retry attempt {retryAttempt}",
          //  ex.GetType().Name,
          //  timeSpan.TotalSeconds * 1000,
          //  retryAttempt
          //);
        }
      );
    var policyResult = await policy
      .ExecuteAndCaptureAsync(async () =>
      {
        Ping myPing = new();
        var hostname =
          Uri.CheckHostName(hostnameOrAddress) != UriHostNameType.Unknown
            ? hostnameOrAddress
            : new Uri(hostnameOrAddress).DnsSafeHost;
        byte[] buffer = new byte[32];
        int timeout = 1000;
        PingOptions pingOptions = new();
        PingReply reply = await myPing.SendPingAsync(hostname, timeout, buffer, pingOptions).ConfigureAwait(false);
        if (reply.Status != IPStatus.Success)
        {
          throw new SpeckleException($"The ping operation failed with status {reply.Status}");
        }

        return true;
      })
      .ConfigureAwait(false);
    if (policyResult.Outcome == OutcomeType.Successful)
    {
      return true;
    }

    SpeckleLog.Logger.Warning(
      policyResult.FinalException,
      "Failed to ping {hostnameOrAddress} cause: {exceptionMessage}",
      policyResult.FinalException.Message
    );
    return false;
  }

  /// <summary>
  /// Sends a <c>GET</c> request to the provided <paramref name="uri"/>
  /// </summary>
  /// <param name="uri">The URI that should be pinged</param>
  /// <exception cref="HttpRequestException">Request to <paramref name="uri"/> failed</exception>
  public static async Task<HttpResponseMessage> HttpPing(Uri uri)
  {
    try
    {
      using var httpClient = GetHttpProxyClient();
      HttpResponseMessage response = await httpClient.GetAsync(uri).ConfigureAwait(false);
      response.EnsureSuccessStatusCode();
      SpeckleLog.Logger.Information("Successfully pinged {uri}", uri);
      return response;
    }
    catch (HttpRequestException ex)
    {
      SpeckleLog.Logger.Warning(ex, "Ping to {uri} was unsuccessful: {message}", uri, ex.Message);
      throw new HttpRequestException($"Ping to {uri} was unsuccessful", ex);
    }
  }

  public static HttpClient GetHttpProxyClient(SpeckleHttpClientHandler? speckleHttpClientHandler = null)
  {
    IWebProxy proxy = WebRequest.GetSystemWebProxy();
    proxy.Credentials = CredentialCache.DefaultCredentials;

    speckleHttpClientHandler ??= new SpeckleHttpClientHandler(new HttpClientHandler(), HttpAsyncPolicy());

    var client = new HttpClient(speckleHttpClientHandler)
    {
      Timeout = Timeout.InfiniteTimeSpan //timeout is configured on the SpeckleHttpClientHandler through policy
    };
    return client;
  }

  public static bool CanAddAuth(string? authToken, out string? bearerHeader)
  {
    if (!string.IsNullOrEmpty(authToken))
    {
      bearerHeader = authToken.NotNull().StartsWith("bearer", StringComparison.InvariantCultureIgnoreCase)
        ? authToken
        : $"Bearer {authToken}";
      return true;
    }

    bearerHeader = null;
    return false;
  }

  public static void AddAuthHeader(HttpClient client, string? authToken)
  {
    if (CanAddAuth(authToken, out string? value))
    {
      client.DefaultRequestHeaders.Add("Authorization", value);
    }
  }
}
