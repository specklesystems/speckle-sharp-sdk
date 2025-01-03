using System.Diagnostics;
using GraphQL;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Credentials;
using Xunit;

namespace Speckle.Sdk.Tests.Unit.Api;

public sealed class GraphQLClientTests : IDisposable
{
  private Client _client;

  public GraphQLClientTests()
  {
    var serviceProvider = TestServiceSetup.GetServiceProvider();
    _client = serviceProvider
      .GetRequiredService<IClientFactory>()
      .Create(
        new Account
        {
          token = "this is a scam",
          serverInfo = new ServerInfo { url = "http://goto.testing" },
        }
      );
  }

  public void Dispose()
  {
    _client?.Dispose();
  }

  [Fact]
  public async Task TestExecuteWithResiliencePoliciesDoesntRetryTaskCancellation()
  {
    var timer = new Stopwatch();
    timer.Start();
    await Assert.ThrowsAsync<TaskCanceledException>(async () =>
    {
      var tokenSource = new CancellationTokenSource();
      tokenSource.Cancel();
      await _client.ExecuteWithResiliencePolicies(
        async () =>
          await Task.Run(
            async () =>
            {
              await Task.Delay(1000);
              return "foo";
            },
            tokenSource.Token
          )
      );
    });
    timer.Stop();
    timer.ElapsedMilliseconds.ShouldBeLessThan(1000);

    // the default retry policy would retry 5 times with 1 second jitter backoff each
    // if the elapsed is less than a second, this was def not retried
  }

  [Fact]
  public async Task TestExecuteWithResiliencePoliciesRetry()
  {
    var counter = 0;
    var maxRetryCount = 5;
    var expectedResult = "finally it finishes";
    var timer = new Stopwatch();
    timer.Start();
    var result = await _client.ExecuteWithResiliencePolicies(() =>
    {
      counter++;
      if (counter < maxRetryCount)
      {
        throw new SpeckleGraphQLInternalErrorException();
      }

      return Task.FromResult(expectedResult);
    });
    timer.Stop();
    // The baseline for wait is 1 seconds between the jittered retry
    timer.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(5000);
    counter.ShouldBe(maxRetryCount);
  }

  public class FakeGqlResponseModel { }
}
