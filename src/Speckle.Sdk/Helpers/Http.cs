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
  /// <exception cref="HttpRequestException">Request to <paramref name="uri"/> failed</exception>
  public async Task<HttpResponseMessage> HttpPing(Uri uri)
  {
    try
    {
      using var httpClient = GetHttpProxyClient();
      HttpResponseMessage response = await httpClient.GetAsync(uri).ConfigureAwait(false);
      response.EnsureSuccessStatusCode();
      logger.LogInformation("Successfully pinged {uri}", uri);
      return response;
    }
    catch (HttpRequestException ex)
    {
      logger.LogWarning(ex, "Ping to {uri} was unsuccessful: {message}", uri, ex.Message);
      throw new HttpRequestException($"Ping to {uri} was unsuccessful", ex);
    }
  }

  public HttpClient GetHttpProxyClient(SpeckleHttpClientHandler? speckleHttpClientHandler = null)
  {
    IWebProxy proxy = WebRequest.GetSystemWebProxy();
    proxy.Credentials = CredentialCache.DefaultCredentials;

    speckleHttpClientHandler ??= speckleHttpClientHandlerFactory.Create();

    var client = new HttpClient(speckleHttpClientHandler)
    {
      Timeout = Timeout.InfiniteTimeSpan //timeout is configured on the SpeckleHttpClientHandler through policy
    };
    return client;
  }

  public bool CanAddAuth(string? authToken, out string? bearerHeader)
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

  public void AddAuthHeader(HttpClient client, string? authToken)
  {
    if (CanAddAuth(authToken, out string? value))
    {
      client.DefaultRequestHeaders.Add("Authorization", value);
    }
  }
}
