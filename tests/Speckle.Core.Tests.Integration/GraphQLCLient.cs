using GraphQL;
using Speckle.Sdk.Api;
using Speckle.Sdk.Credentials;

namespace Speckle.Core.Tests.Integration;

public class GraphQLClientTests : IDisposable
{
  private Account _account;
  private Client _client;

  [OneTimeSetUp]
  public async Task Setup()
  {
    _account = await Fixtures.SeedUser();
    _client = new Client(_account);
  }

  [Test]
  public void ThrowsForbiddenException()
  {
    Assert.ThrowsAsync<SpeckleGraphQLForbiddenException>(
      async () =>
        await _client.ExecuteGraphQLRequest<Dictionary<string, object>>(
          new GraphQLRequest
          {
            Query =
              @"query {
            adminStreams{
              totalCount
              }
            }"
          }
        )
    );
  }

  [Test]
  public void Cancellation()
  {
    using CancellationTokenSource tokenSource = new();
    tokenSource.Cancel();
    Assert.CatchAsync<OperationCanceledException>(
      async () =>
        await _client.ExecuteGraphQLRequest<Dictionary<string, object>>(
          new GraphQLRequest
          {
            Query =
              @"query {
            adminStreams{
              totalCount
              }
            }"
          },
          tokenSource.Token
        )
    );
  }

  public void Dispose()
  {
    _client?.Dispose();
  }
}
