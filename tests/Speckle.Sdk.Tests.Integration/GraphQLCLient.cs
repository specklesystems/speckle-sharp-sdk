using GraphQL;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk.Api;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Tests.Integration;

public class GraphQLClientTests : IDisposable
{
  private Account _account;
  private Client _client;
  private IOperations _operations;

  [SetUp]
  public async Task Setup()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(DataChunk).Assembly);
    var serviceProvider = TestServiceSetup.GetServiceProvider();
    _operations = serviceProvider.GetRequiredService<IOperations>();
    _account = await Fixtures.SeedUser();
    _client = serviceProvider.GetRequiredService<IClientFactory>().Create(_account);
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
