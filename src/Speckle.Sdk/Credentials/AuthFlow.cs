using System.Diagnostics;
using System.Diagnostics.Contracts;
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
  private readonly JsonSerializerSettings _serializerSettings = new()
  {
    MissingMemberHandling = MissingMemberHandling.Error,
    NullValueHandling = NullValueHandling.Ignore,
  };

  public async Task<TokenExchangeResponse> TriggerAuthFlowWithTimeout(
    Uri serverUrl,
    AuthApp authApp,
    TimeSpan timeout,
    CancellationToken cancellationToken
  )
  {
    using HttpClient client = speckleHttp.CreateHttpClient();

    Uri tokenEndpoint = new(serverUrl, "/oauth/token");
    string codeVerifier = GenerateCodeVerifier();
    Uri authnVerify;
    using var req = await client.GetAsync(tokenEndpoint, cancellationToken).ConfigureAwait(false);
    bool useLegacyEndpoint = req.StatusCode != HttpStatusCode.OK;

    if (useLegacyEndpoint)
    {
      string challenge = codeVerifier; // Old endpoint only supports PKCE "plain" method
      authnVerify = new($"/authn/verify/{authApp.AppId}/{challenge}", UriKind.Relative);
      tokenEndpoint = new(serverUrl, "/auth/token");
    }
    else
    {
      string challenge = GenerateCodeChallenge(codeVerifier);
      authnVerify = new($"/authn/verify/{authApp.AppId}/{challenge}?code_challenge_method=S256", UriKind.Relative);
    }

    Uri endpoint = new(serverUrl, authnVerify);
    _ = Process.Start(new ProcessStartInfo(endpoint.ToString()) { UseShellExecute = true });
    string accessCode = await RunListenerWithTimeout(authApp.CallbackUrl, timeout, cancellationToken)
      .ConfigureAwait(false);

    object body = useLegacyEndpoint
      ? new
      {
        appId = authApp.AppId,
        appSecret = authApp.AppSecret,
        accessCode = accessCode,
        challenge = codeVerifier,
      }
      : new
      {
        appId = authApp.AppId,
        accessCode = accessCode,
        codeVerifier = codeVerifier,
      };

    return await ExchangeAccessCodeForToken(
        client,
        JsonConvert.SerializeObject(body, _serializerSettings),
        tokenEndpoint,
        cancellationToken
      )
      .ConfigureAwait(false);
  }

  /// <summary>
  ///
  /// </summary>
  /// <param name="applicationCallbackUrl"></param>
  /// <param name="timeout"></param>
  /// <param name="userCancellation"></param>
  /// <returns></returns>
  /// <exception cref="OperationCanceledException"><paramref name="userCancellation"/> requested cancel</exception>
  /// <exception cref="TimeoutException">timeout was reached</exception>
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
      throw new TimeoutException($"Auth flow was cancelled after {timeout:g} timeout", ex);
    }
  }

  /// <summary>
  ///
  /// </summary>
  /// <param name="refreshToken"></param>
  /// <param name="serverUrl"></param>
  /// <param name="authApp">Auth app, needs to match the app that generated the refresh token originally</param>
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

    using var content = new StringContent(JsonConvert.SerializeObject(body, _serializerSettings));
    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
    var response = await client
      .PostAsync(new Uri(serverUrl, "/auth/token"), content, cancellationToken)
      .ConfigureAwait(false);
    response.EnsureSuccessStatusCode();

#if NET8_0_OR_GREATER
    string read = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
    string read = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
    return JsonConvert.DeserializeObject<TokenExchangeResponse>(read, _serializerSettings).NotNull();
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

  public static async Task<string> RunListener(Uri localUrl, CancellationToken cancellationToken)
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
      throw new AuthFlowException("Authentication flow was denied"); //denied presumably by the user
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

  private async Task<TokenExchangeResponse> ExchangeAccessCodeForToken(
    HttpClient client,
    string jsonContent,
    Uri tokenEndpoint,
    CancellationToken cancellationToken
  )
  {
    using StringContent content = new(jsonContent);
    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

    using HttpResponseMessage response = await client
      .PostAsync(tokenEndpoint, content, cancellationToken)
      .ConfigureAwait(false);
    response.EnsureSuccessStatusCode();

#if NET8_0_OR_GREATER
    string read = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
    string read = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif

    return JsonConvert.DeserializeObject<TokenExchangeResponse>(read, _serializerSettings).NotNull();
  }

  [Pure]
  public static string GenerateCodeVerifier()
  {
#if NET8_0_OR_GREATER
    Span<byte> codeVerifierData = stackalloc byte[32];
    RandomNumberGenerator.Fill(codeVerifierData);
#else
    using RNGCryptoServiceProvider rng = new();
    byte[] codeVerifierData = new byte[32];
    rng.GetBytes(codeVerifierData);
#endif

    return Base64UrlEncode(codeVerifierData);
  }

  [Pure]
  public static string GenerateCodeChallenge(string codeVerifier)
  {
#if NET8_0_OR_GREATER
    int byteCount = Encoding.UTF8.GetByteCount(codeVerifier.AsSpan());
    Span<byte> codeVerifierBytes = stackalloc byte[byteCount];
    Encoding.UTF8.GetBytes(codeVerifier, codeVerifierBytes);
    Span<byte> challengeData = stackalloc byte[SHA256.HashSizeInBytes];
    SHA256.HashData(codeVerifierBytes, challengeData);
#else
    byte[] codeVerifierBytes = Encoding.UTF8.GetBytes(codeVerifier);
    using SHA256 hash = SHA256.Create();
    byte[] challengeData = hash.ComputeHash(codeVerifierBytes);
#endif
    return Base64UrlEncode(challengeData);
  }

  [Pure]
  private static string Base64UrlEncode(
#if NET8_0_OR_GREATER
    ReadOnlySpan<byte> bytes
#else
    byte[] bytes
#endif
  )
  {
    // Base64Url is available in .NET 9, or via the Microsoft.Bcl.Memory polyfill
    // But for simplicity r.e. dll dependencies, we're doing it the dumb way...
    return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
  }
}
