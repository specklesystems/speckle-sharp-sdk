using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.Blob;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Testing;

namespace Speckle.Sdk.Tests.Unit.Api.GraphQL;

public class SubscriptionReconnectTests : MoqTest
{
  [Theory]
  [InlineData(SocketTeardown.Abort)]
  [InlineData(SocketTeardown.CleanClose)]
  public async Task SubscribeTo_Reconnects_WhenTheServerDropsTheSocket(SocketTeardown teardown)
  {
    using var server = new FakeGraphQLWebSocketServer(teardown);

    var account = new Account
    {
      token = "token",
      serverInfo = new ServerInfo { url = server.ServerUrl.AbsoluteUri },
    };

    var speckleHttp = Create<ISpeckleHttp>();
    speckleHttp.Setup(x => x.CreateHttpClient(null, 30, account.token)).Returns(new HttpClient());

    var application = new SpeckleApplication
    {
      HostApplication = "Tests",
      HostApplicationVersion = "1",
      Slug = "tests",
      SpeckleVersion = "1",
    };

    IGraphQLClientFactory graphqlClientFactory = new GraphQLClientFactory(
      application,
      speckleHttp.Object,
      Create<ILogger<GraphQLClientFactory>>(MockBehavior.Loose).Object
    );

    using var client = new Client(
      Create<ILogger<Client>>(MockBehavior.Loose).Object,
      Create<ISdkActivityFactory>(MockBehavior.Loose).Object,
      graphqlClientFactory,
      Create<IBlobApiFactory>(MockBehavior.Loose).Object,
      account
    );

    using var subscription = client.Subscription.CreateUserProjectsUpdatedSubscription();

    var reconnected = await server.WaitForConnectionsAsync(2, TimeSpan.FromSeconds(30));

    reconnected.Should().BeTrue();
    server.ConnectionCount.Should().BeGreaterThanOrEqualTo(2);
  }
}
