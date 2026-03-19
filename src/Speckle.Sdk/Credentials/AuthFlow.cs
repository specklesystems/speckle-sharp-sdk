using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Speckle.InterfaceGenerator;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Common;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Logging;

namespace Speckle.Sdk.Credentials;

/// <summary>
/// Authentication flow with the Speckle Server to create a application token for the <c>connectorsV3</c> application
/// Starts the browser based authentication flow where the user's browser will be opened, they'll be asked to
/// confirm permission, then an access code will be given via a <see cref="HttpListener"/> which will be exchanged
/// for a <see cref="TokenExchangeResponse"/>
/// </summary>
/// <remarks>
/// Note, this class is not coupled in any way to <see cref="Account"/>
/// lets keep it that way...
/// See instead <see cref="AccountManager"/>
/// </remarks>
[GenerateAutoInterface]
public sealed class AuthFlow(ISdkActivityFactory activityFactory, ISpeckleHttp speckleHttp) : IAuthFlow
{
  public async Task<TokenExchangeResponse> TriggerAuthFlowWithTimeout(
    Uri serverUrl,
    AuthApp authApp,
    TimeSpan timeout,
    CancellationToken cancellationToken
  )
  {
    string challenge = GenerateChallenge();

    Uri endpoint = new(serverUrl, $"/authn/verify/{authApp.AppId}/{challenge}");
    _ = Process.Start(new ProcessStartInfo(endpoint.ToString()) { UseShellExecute = true });
    string accessCode = await RunListenerWithTimeout(authApp.CallbackUrl, timeout, cancellationToken)
      .ConfigureAwait(false);

    return await ExchangeAccessCodeForToken(accessCode, authApp, challenge, serverUrl, cancellationToken)
      .ConfigureAwait(false);
  }

  public async Task<string> RunListenerWithTimeout(
    Uri applicationCallbackUrl,
    TimeSpan timeout,
    CancellationToken userCancellation
  )
  {
    using CancellationTokenSource cancelOnTimeout = new(timeout);
    using CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
      cancelOnTimeout.Token,
      userCancellation
    );

    try
    {
      using var activity = activityFactory.Start("Listening for authflow access code");

      return await RunListener(applicationCallbackUrl, linkedSource.Token).ConfigureAwait(false);
    }
    catch (OperationCanceledException) when (userCancellation.IsCancellationRequested)
    {
      throw;
    }
    catch (OperationCanceledException ex) when (cancelOnTimeout.IsCancellationRequested)
    {
      throw new AuthFlowException($"Auth flow was cancelled after {timeout:g} timeout", ex);
    }
  }

  /// <summary>
  ///
  /// </summary>
  /// <param name="refreshToken"></param>
  /// <param name="serverUrl"></param>
  /// <param name="authApp"></param>
  /// <param name="cancellationToken"></param>
  /// <exception cref="HttpRequestException">HTTP exceptions</exception>
  /// <exception cref="JsonSerializationException">Server response was invalid or partial</exception>
  /// <exception cref="ArgumentOutOfRangeException ">Invalid <paramref name="serverUrl"/> (must be absolute url)</exception>
  /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> requested cancel</exception>
  /// <returns></returns>
  public async Task<TokenExchangeResponse> GetRefreshedToken(
    string? refreshToken,
    Uri serverUrl,
    AuthApp authApp,
    CancellationToken cancellationToken
  )
  {
    using var client = speckleHttp.CreateHttpClient();

    var body = new
    {
      appId = authApp.AppId,
      appSecret = authApp.AppSecret,
      refreshToken = refreshToken,
    };

    using var content = new StringContent(JsonConvert.SerializeObject(body));
    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
    var response = await client
      .PostAsync(new Uri(serverUrl, "/auth/token"), content, cancellationToken)
      .ConfigureAwait(false);

#if NET8_0_OR_GREATER
    string read = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
    string read = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
    return JsonConvert.DeserializeObject<TokenExchangeResponse>(read).NotNull();
  }

  private static async Task<HttpListenerContext> GetContext(HttpListener listener, CancellationToken cancellationToken)
  {
    //GetContextAsync doesn't support cancellation, so we have to do this song and dance...
    Task timeoutTask = Task.Delay(Timeout.Infinite, cancellationToken);
    Task<HttpListenerContext> getContextTask = listener.GetContextAsync();

    Task completed = await Task.WhenAny(getContextTask, timeoutTask).ConfigureAwait(false);
    if (completed == getContextTask)
    {
      return getContextTask.Result;
    }

    cancellationToken.ThrowIfCancellationRequested();

    throw new InvalidOperationException("Cancellation should have thrown, this shouldn't be possible");
  }

  internal static async Task<string> RunListener(Uri localUrl, CancellationToken cancellationToken)
  {
    using HttpListener listener = new();
    listener.Prefixes.Add(localUrl.ToString());
    listener.Start();

    HttpListenerContext context = await GetContext(listener, cancellationToken).ConfigureAwait(false);
    HttpListenerRequest request = context.Request;
    using HttpListenerResponse response = context.Response;

    string? accessCode = request.QueryString["access_code"];
    string? denied = request.QueryString["denied"];
    bool isDenied = denied == "true";

    if (isDenied)
    {
      //lang=html
      WriteResponse(
        """
        <h1>Denied!</h1>
        <br/><br/>
        Please close this window and return to your Speckle Connector.
        """
      );
      throw new AuthFlowException("Authentication flow was denied");
    }
    else if (accessCode != null)
    {
      //lang=html
      WriteResponse(
        """
        <h1>Success!</h1>
        <br/><br/>
        Your Speckle Connector is now authorized
        <br/><br/>
        You may now close this window and return to your Speckle Connector
        """
      );
      return accessCode;
    }
    else
    {
      //lang=html
      WriteResponse(
        """
        <h1>Failed!</h1>
        <br/><br/>
        Something went wrong trying to authorize your Speckle Connector
        <br/><br/>
        Please close this window and try again from your Speckle Connector.
        """
      );
      throw new AuthFlowException("Failed to receive access code");
    }

    void WriteResponse(string message)
    {
      //lang=html
      string responseString = $"""
        <HTML>
            <BODY Style='background: #FAFAFAFF; font-family: Inter, Roboto, sans-serif; font-size: 1rem; font-weight: 500; text-align: center;'>
                <br/>
                {message}
            </BODY>
        </HTML>
        """;

      byte[] buffer = Encoding.UTF8.GetBytes(responseString);
      response.ContentLength64 = buffer.Length;
      response.OutputStream.Write(buffer, 0, buffer.Length);
    }
  }

  public async Task<TokenExchangeResponse> ExchangeAccessCodeForToken(
    string accessCode,
    AuthApp authApp,
    string challenge,
    Uri serverUrl,
    CancellationToken cancellationToken
  )
  {
    using HttpClient client = speckleHttp.CreateHttpClient();

    var body = new
    {
      appId = authApp.AppId,
      appSecret = authApp.AppSecret,
      accessCode = accessCode,
      challenge = challenge,
    };

    using StringContent content = new(JsonConvert.SerializeObject(body));
    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

    using HttpResponseMessage response = await client
      .PostAsync(new Uri(serverUrl, "/auth/token"), content, cancellationToken)
      .ConfigureAwait(false);
    response.EnsureSuccessStatusCode();

#if NET8_0_OR_GREATER
    string read = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
    string read = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif

    return JsonConvert.DeserializeObject<TokenExchangeResponse>(read).NotNull();
  }

  public static string GenerateChallenge()
  {
#if NET8_0_OR_GREATER
    Span<byte> challengeData = stackalloc byte[32];
    RandomNumberGenerator.Fill(challengeData);
#else
    using RNGCryptoServiceProvider rng = new();
    byte[] challengeData = new byte[32];
    rng.GetBytes(challengeData);
#endif
    // Base64Url is available in .NET 9, or via the Microsoft.Bcl.Memory polyfill
    // But for simplicity r.e. dll dependencies, we're doing it the dumb way...
    return Convert.ToBase64String(challengeData).Replace('+', '-').Replace('/', '_').TrimEnd('=');
  }
}
