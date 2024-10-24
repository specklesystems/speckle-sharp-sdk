using System.Net;
using Microsoft.Extensions.Logging;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Common;

namespace Speckle.Sdk.Helpers;

[GenerateAutoInterface]
public class SpeckleHttp(ILogger<SpeckleHttp> logger, ISpeckleHttpClientHandlerFactory speckleHttpClientHandlerFactory)
  : ISpeckleHttp
{
  /// <summary>
  /// Sends a <c>GET</c> request to the provided <paramref name="uri"/>
  /// </summary>
  /// <param name="uri">The URI that should be pinged</param>
  /// <exception cref="System.Net.Http.HttpRequestException">Request to <paramref name="uri"/> failed</exception>
  public async Task<System.Net.Http.HttpResponseMessage> HttpPing(Uri uri)
  {
    try
    {
      using var httpClient = CreateHttpClient();
      System.Net.Http.HttpResponseMessage response = await httpClient.GetAsync(uri).ConfigureAwait(false);
      response.EnsureSuccessStatusCode();
      logger.LogInformation("Successfully pinged {uri}", uri);
      return response;
    }
    catch (System.Net.Http.HttpRequestException ex)
    {
      logger.LogWarning(ex, "Ping to {uri} was unsuccessful: {message}", uri, ex.Message);
      throw new System.Net.Http.HttpRequestException($"Ping to {uri} was unsuccessful", ex);
    }
  }

  public const int DEFAULT_TIMEOUT_SECONDS = 60;

  public System.Net.Http.HttpClient CreateHttpClient(
    System.Net.Http.HttpMessageHandler? innerHandler = null,
    int timeoutSeconds = DEFAULT_TIMEOUT_SECONDS,
    string? authorizationToken = null
  )
  {
    IWebProxy proxy = WebRequest.GetSystemWebProxy();
    proxy.Credentials = CredentialCache.DefaultCredentials;

    var speckleHandler = speckleHttpClientHandlerFactory.Create(innerHandler, timeoutSeconds);

    var client = new System.Net.Http.HttpClient(speckleHandler)
    {
      Timeout =
        Timeout.InfiniteTimeSpan //timeout is configured on the SpeckleHttpClientHandler through policy
      ,
    };
    AddAuthHeader(client, authorizationToken);
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

  private static void AddAuthHeader(System.Net.Http.HttpClient client, string? authToken)
  {
    if (CanAddAuth(authToken, out string? value))
    {
      client.DefaultRequestHeaders.Add("Authorization", value);
    }
  }
}
