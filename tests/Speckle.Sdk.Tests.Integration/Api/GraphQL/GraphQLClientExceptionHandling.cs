using System.ComponentModel;
using FluentAssertions;
using GraphQL;
using GraphQL.Client.Http;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Inputs;

namespace Speckle.Sdk.Tests.Integration.Api.GraphQL;

public class GraphQLClientExceptionHandling : IAsyncLifetime
{
  private Client _sut;

  public Task DisposeAsync() => Task.CompletedTask;

  public async Task InitializeAsync()
  {
    _sut = await Fixtures.SeedUserWithClient();
  }

  [Fact]
  [Description($"Attempts to execute a query on a non-existent server, expect a {nameof(GraphQLHttpRequestException)}")]
  public async Task TestHttpLayer()
  {
    _sut.GQLClient.Options.EndPoint = new Uri("http://127.0.0.1:1234"); //There is no server on this port...

    await Assert.ThrowsAsync<HttpRequestException>(async () => await _sut.ActiveUser.Get().ConfigureAwait(false));
  }

  [Fact]
  [Description(
    $"Attempts to execute a admin only command from a regular user, expect an inner {nameof(SpeckleGraphQLForbiddenException)}"
  )]
  public async Task TestGraphQLLayer_Forbidden()
  {
    //language=graphql
    const string QUERY = """
      query Query {
        admin {
          userList {
            items {
              id
            }
          }
        }
      }

      """;
    GraphQLRequest request = new(query: QUERY);
    var ex = await Assert.ThrowsAsync<AggregateException>(
      async () => await _sut.ExecuteGraphQLRequest<dynamic>(request).ConfigureAwait(false)
    );
    ex.InnerExceptions.OfType<SpeckleGraphQLForbiddenException>().Count().Should().Be(1);
  }

  [Fact, Description($"Attempts to execute a bad query, expect an inner {nameof(SpeckleGraphQLInvalidQueryException)}")]
  public async Task TestGraphQLLayer_BadQuery()
  {
    //language=graphql
    const string QUERY = """
       query User {
        data:NonExistentQuery {
          id
        }
      }
      """;
    GraphQLRequest request = new(query: QUERY);
    var ex = await Assert.ThrowsAsync<AggregateException>(
      async () => await _sut.ExecuteGraphQLRequest<dynamic>(request).ConfigureAwait(false)
    );
    ex.InnerExceptions.OfType<SpeckleGraphQLInvalidQueryException>().Count().Should().Be(1);
  }

  [Fact]
  [Description(
    $"Attempts to execute a query with an invalid input, expect an inner {nameof(SpeckleGraphQLBadInputException)}"
  )]
  public async Task TestGraphQLLayer_BadInput()
  {
    ProjectUpdateRoleInput input = new(null!, null!, null);
    var ex = await Assert.ThrowsAsync<AggregateException>(
      async () => await _sut.Project.UpdateRole(input).ConfigureAwait(false)
    );
    ex.InnerExceptions.OfType<SpeckleGraphQLBadInputException>().Count().Should().Be(2);
  }

  [Fact]
  public async Task TestCancel()
  {
    using CancellationTokenSource cts = new();
    await cts.CancelAsync();

    var ex = await Assert.ThrowsAsync<TaskCanceledException>(
      async () => await _sut.ActiveUser.Get(cts.Token).ConfigureAwait(false)
    );

    ex.CancellationToken.Should().BeEquivalentTo(cts.Token);
  }

  [Fact]
  public void TestDisposal()
  {
    _sut.Dispose();

    Assert.Throws<ObjectDisposedException>(() => _ = _sut.Subscription.CreateUserProjectsUpdatedSubscription());
  }

  [
    Fact,
    Description($"Attempts to execute a query with a mismatched type, expect an {nameof(JsonSerializationException)}")
  ]
  public async Task TestDeserialization()
  {
    //language=graphql
    const string QUERY = """
       query User {
        data:activeUser {
          id
        }
      }
      """;
    GraphQLRequest request = new(query: QUERY);
    await Assert.ThrowsAsync<JsonReaderException>(
      async () => await _sut.ExecuteGraphQLRequest<int>(request).ConfigureAwait(false)
    );
  }
}
