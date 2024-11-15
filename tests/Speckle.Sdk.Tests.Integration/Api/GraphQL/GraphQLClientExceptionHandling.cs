using GraphQL;
using GraphQL.Client.Http;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Inputs;

namespace Speckle.Sdk.Tests.Integration.Api.GraphQL;

[TestOf(typeof(Client))]
public class GraphQLClientExceptionHandling
{
  private Client _sut;

  [SetUp]
  public async Task Setup()
  {
    _sut = await Fixtures.SeedUserWithClient();
  }

  [Test]
  [Description($"Attempts to execute a query on a non-existent server, expect a {nameof(GraphQLHttpRequestException)}")]
  public void TestHttpLayer()
  {
    _sut.GQLClient.Options.EndPoint = new Uri("http://127.0.0.1:1234"); //There is no server on this port...

    Assert.ThrowsAsync<HttpRequestException>(async () => await _sut.ActiveUser.Get().ConfigureAwait(false));
  }

  [Test]
  [Description(
    $"Attempts to execute a admin only command from a regular user, expect an inner {nameof(SpeckleGraphQLForbiddenException)}"
  )]
  public void TestGraphQLLayer_Forbidden()
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
    var ex = Assert.ThrowsAsync<AggregateException>(
      async () => await _sut.ExecuteGraphQLRequest<dynamic>(request).ConfigureAwait(false)
    );
    Assert.That(ex?.InnerExceptions, Has.Exactly(1).TypeOf<SpeckleGraphQLForbiddenException>());
  }

  [Test, Description($"Attempts to execute a bad query, expect an inner {nameof(SpeckleGraphQLInvalidQueryException)}")]
  public void TestGraphQLLayer_BadQuery()
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
    var ex = Assert.ThrowsAsync<AggregateException>(
      async () => await _sut.ExecuteGraphQLRequest<dynamic>(request).ConfigureAwait(false)
    );

    Assert.That(ex?.InnerExceptions, Has.Exactly(1).TypeOf<SpeckleGraphQLInvalidQueryException>());
  }

  [Test]
  [Description(
    $"Attempts to execute a query with an invalid input, expect an inner {nameof(SpeckleGraphQLBadInputException)}"
  )]
  public void TestGraphQLLayer_BadInput()
  {
    ProjectUpdateRoleInput input = new(null!, null!, null);
    var ex = Assert.ThrowsAsync<AggregateException>(
      async () => await _sut.Project.UpdateRole(input).ConfigureAwait(false)
    );

    Assert.That(ex?.InnerExceptions, Has.Exactly(2).TypeOf<SpeckleGraphQLBadInputException>());
  }

  [Test]
  public void TestCancel()
  {
    using CancellationTokenSource cts = new();
    cts.Cancel();

    var ex = Assert.CatchAsync<OperationCanceledException>(
      async () => await _sut.ActiveUser.Get(cts.Token).ConfigureAwait(false)
    );

    Assert.That(ex?.CancellationToken, Is.EqualTo(cts.Token));
  }

  [Test]
  public void TestDisposal()
  {
    _sut.Dispose();

    Assert.Throws<ObjectDisposedException>(() => _ = _sut.Subscription.CreateUserProjectsUpdatedSubscription());
  }

  [
    Test,
    Description($"Attempts to execute a query with a mismatched type, expect an {nameof(JsonSerializationException)}")
  ]
  public void TestDeserialization()
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
    Assert.CatchAsync<JsonException>(async () => await _sut.ExecuteGraphQLRequest<int>(request).ConfigureAwait(false));
  }
}
