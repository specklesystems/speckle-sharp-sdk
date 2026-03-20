using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Api;
using Speckle.Sdk.Credentials;

namespace Speckle.Sdk.Tests.Integration.Credentials;

public class AuthFlowExceptionalTests : IAsyncLifetime
{
  private IAuthFlow _authFlow;
  private IClient _client;
  private readonly Uri _url = AuthApp.ConnectorsV3.CallbackUrl;

  [Fact]
  public async Task GetRefreshToken_Cancellation()
  {
    await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
      _ = await _authFlow.GetRefreshedToken(
        _client.Account.refreshToken,
        _client.ServerUrl,
        Fixtures.TestAuthApp,
        new(true)
      )
    );
  }

  [Fact]
  public async Task GetRefreshToken_UnknownApp()
  {
    //interestingly, the server responds with a 401 Unauthorized despite internally being a bad request
    await Assert.ThrowsAnyAsync<HttpRequestException>(async () =>
      _ = await _authFlow.GetRefreshedToken(
        _client.Account.refreshToken,
        _client.ServerUrl,
        new()
        {
          AppId = "doesn't exist",
          AppSecret = "doesn't exist",
          CallbackUrl = new("invalid://localhost"),
        },
        CancellationToken.None
      )
    );
  }

  [Fact]
  public async Task GetRefreshToken_NullRefreshToken()
  {
    await Assert.ThrowsAnyAsync<HttpRequestException>(async () =>
      _ = await _authFlow.GetRefreshedToken(null, _client.ServerUrl, AuthApp.ConnectorsV3, CancellationToken.None)
    );
  }

  [Fact]
  public async Task SimultaneousListeners_SamePort_OneFails()
  {
    using CancellationTokenSource ct = new();
    var task1 = AuthFlow.RunListener(_url, ct.Token);
    await Task.Delay(50, CancellationToken.None);

    await Assert.ThrowsAsync<HttpListenerException>(async () => await AuthFlow.RunListener(_url, ct.Token));

    Assert.False(task1.IsCompleted);
    await ct.CancelAsync();
    await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task1);
  }

  public async Task InitializeAsync()
  {
    _authFlow = Fixtures.ServiceProvider.GetRequiredService<IAuthFlow>();
    _client = await Fixtures.SeedUserWithClient();
  }

  public Task DisposeAsync()
  {
    _client.Dispose();
    return Task.CompletedTask;
  }
}
