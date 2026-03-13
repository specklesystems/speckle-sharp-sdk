using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk.Credentials;

namespace Speckle.Sdk.Tests.Integration.Credentials;

public sealed class AuthFlowTests
{
  private readonly AuthFlow _authFlow;
  private readonly Uri _url = new("http://localhost:29355/");

  public AuthFlowTests()
  {
    _authFlow = (AuthFlow)Fixtures.ServiceProvider.GetRequiredService<IAuthFlow>();
  }

  [Fact]
  public async Task RunListener_ReturnsAccessCode_WhenQueryContainsAccessCode()
  {
    var listenerTask = AuthFlow.RunListener(_url, CancellationToken.None);
    using var client = new HttpClient();
    const string EXPECTED_ACCESS_CODE = "abcdef123456";

    var response = await client.GetAsync(new Uri(_url, $"?access_code={EXPECTED_ACCESS_CODE}"));
    response.EnsureSuccessStatusCode();

    string result = await listenerTask;

    Assert.Equal(EXPECTED_ACCESS_CODE, result);
  }

  [Fact]
  public async Task RunListener_Throws_InvalidAccessCode()
  {
    var listenerTask = AuthFlow.RunListener(_url, CancellationToken.None);
    using var client = new HttpClient();

    var response = await client.GetAsync(new Uri(_url, ""));
    response.EnsureSuccessStatusCode();

    await Assert.ThrowsAsync<AuthFlowException>(async () =>
    {
      _ = await listenerTask;
    });
  }

  [Fact]
  public async Task RunListener_Throws_Cancellation()
  {
    using CancellationTokenSource cancellationTokenSource = new();
    var listenerTask = AuthFlow.RunListener(_url, cancellationTokenSource.Token);

    await cancellationTokenSource.CancelAsync();

    await Assert.ThrowsAsync<OperationCanceledException>(async () =>
    {
      _ = await listenerTask;
    });
  }

  [Theory]
  [InlineData(0.1)]
  [InlineData(1)]
  [InlineData(5)]
  public async Task RunListener_Timeout(double timeS)
  {
    await Assert.ThrowsAsync<AuthFlowException>(async () =>
    {
      _ = await _authFlow.RunListenerWithTimeout(_url, TimeSpan.FromSeconds(timeS), CancellationToken.None);
    });
  }
}
