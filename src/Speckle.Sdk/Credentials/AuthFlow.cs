using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Speckle.InterfaceGenerator;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Common;
using Speckle.Sdk.Dependencies;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Logging;

namespace Speckle.Sdk.Credentials;

/// <summary>
/// Authentication flow with the Speckle Server to create a application token for the <c>connectorsV3</c> application
/// Starts the browser based authentication flow where the user's browser will be opened, they'll be asked to
/// confirm permission, then an access code will be given via a <see cref="HttpListener"/> which will be exchanged
/// for a <see cref="TokenExchangeResponse"/>
/// </summary>
[GenerateAutoInterface]
public sealed class AuthFlow(ISdkActivityFactory activityFactory, ISpeckleHttp speckleHttp) : IAuthFlow
{
  public async Task<TokenExchangeResponse> TriggerAuthFlowWithTimeout(
    Uri serverUrl,
    string applicationSecret,
    Uri applicationCallbackUrl,
    TimeSpan timeout,
    CancellationToken cancellationToken
  )
  {
    string challenge = GenerateChallenge();

    Uri endpoint = new(serverUrl, $"/authn/verify/{applicationSecret}/{challenge}");
    _ = Process.Start(new ProcessStartInfo(endpoint.ToString()) { UseShellExecute = true });
    string accessCode = await RunListenerWithTimeout(applicationCallbackUrl, timeout, cancellationToken)
      .ConfigureAwait(false);

    return await GetToken(accessCode, applicationSecret, challenge, serverUrl, cancellationToken).ConfigureAwait(false);
  }

  internal async Task<string> RunListenerWithTimeout(
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

    string message =
      accessCode != null
        ?
        //lang=html
        "Success!<br/><br/>You can close this window now.<script>window.close();</script>"
        //lang=html
        : "Oups, something went wrong...!";

    //lang=html
    string responseString =
      $"<HTML><BODY Style='background: linear-gradient(to top right, #ffffff, #c8e8ff); font-family: Roboto, sans-serif; font-size: 2rem; font-weight: 500; text-align: center;'><br/>{message}</BODY></HTML>";

    byte[] buffer = Encoding.UTF8.GetBytes(responseString);
    response.ContentLength64 = buffer.Length;
    response.OutputStream.Write(buffer, 0, buffer.Length);

    return accessCode ?? throw new AuthFlowException("Failed to receive access code");
  }

  private async Task<TokenExchangeResponse> GetToken(
    string accessCode,
    string appSecret,
    string challenge,
    Uri serverUrl,
    CancellationToken cancellationToken
  )
  {
    using HttpClient client = speckleHttp.CreateHttpClient();

    var body = new
    {
      appId = appSecret,
      appSecret,
      accessCode,
      challenge,
    };

    using StringContent content = new(JsonConvert.SerializeObject(body));
    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

    using HttpResponseMessage response = await client
      .PostAsync(new Uri(serverUrl, "/auth/token"), content, cancellationToken)
      .ConfigureAwait(false);

#if NET8_0_OR_GREATER
    string read = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
    string read = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
    return JsonConvert.DeserializeObject<TokenExchangeResponse>(read).NotNull();
  }

  internal static string GenerateChallenge()
  {
#if NET8_0_OR_GREATER
    Span<byte> challengeData = stackalloc byte[32];
    RandomNumberGenerator.Fill(challengeData);
#else
    using RNGCryptoServiceProvider rng = new();
    byte[] challengeData = new byte[32];
    rng.GetBytes(challengeData);
#endif
    return Base64Url.EncodeToString(challengeData);
  }
}
