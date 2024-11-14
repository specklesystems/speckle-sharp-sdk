using System.Diagnostics;
using GraphQL;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Credentials;

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

  public static IEnumerable<(Type, Map)> ErrorCases()
  {
    yield return (typeof(SpeckleGraphQLForbiddenException), new Map { { "code", "FORBIDDEN" } });
    yield return (typeof(SpeckleGraphQLForbiddenException), new Map { { "code", "UNAUTHENTICATED" } });
    yield return (
      typeof(SpeckleGraphQLInternalErrorException),
      new Map { { "code", "INTERNAL_SERVER_ERROR" } }
    );
    yield return (typeof(SpeckleGraphQLException<FakeGqlResponseModel>), new Map { { "foo", "bar" } });
  }

  [Test, MethodDataSource(typeof(GraphQLClientTests), nameof(ErrorCases))]
  public void TestExceptionThrowingFromGraphQLErrors(Type exType, Map extensions)
  {
    Assert.Throws(
      exType,
      () =>
        _client.MaybeThrowFromGraphQLErrors(
          new GraphQLRequest(),
          new GraphQLResponse<FakeGqlResponseModel>
          {
            Errors = new GraphQLError[] { new() { Extensions = extensions } },
          }
        )
    );
  }

  [Test]
  public void TestMaybeThrowsDoesntThrowForNoErrors()
  {
    _client.MaybeThrowFromGraphQLErrors(new GraphQLRequest(), new GraphQLResponse<string>());
  }

  [Test]
  public async Task TestExecuteWithResiliencePoliciesDoesntRetryTaskCancellation()
  {
    var timer = new Stopwatch();
    timer.Start();
    await Assert.ThrowsAsync<TaskCanceledException>(async () =>
    {
      var tokenSource = new CancellationTokenSource();
#pragma warning disable CA1849
      tokenSource.Cancel();
#pragma warning restore CA1849
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
    var elapsed = timer.ElapsedMilliseconds;

    // the default retry policy would retry 5 times with 1 second jitter backoff each
    // if the elapsed is less than a second, this was def not retried
    elapsed.ShouldBeLessThan(1000);
  }

  [Test]
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
        throw new SpeckleGraphQLInternalErrorException(new GraphQLRequest(), new GraphQLResponse<string>());
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
