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
      return response;
    }
    catch (HttpRequestException ex)
    {
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
      bearerHeader = authToken.NotNull().ToLowerInvariant().Contains("bearer") ? authToken : $"Bearer {authToken}";
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
