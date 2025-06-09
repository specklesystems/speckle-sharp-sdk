using FluentAssertions;
using GraphQL;
using GraphQL.Client.Http;
using Microsoft.Extensions.Logging;
using Moq;
using RichardSzalay.MockHttp;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Serializer;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Testing;

namespace Speckle.Sdk.Tests.Unit.Api.GraphQL;

public class ClientTests : MoqTest
{
  [Fact]
  //basic end to end GraphQL test as is.  Avoids a proper request/response
  public async Task ExecuteGraphQLRequest()
  {
    using var mockHandler = new MockHttpMessageHandler();
    mockHandler.When(HttpMethod.Post, "https://speckle.xyz/graphql").Respond("application/json", "{}");
    var httpClient = mockHandler.ToHttpClient();
    var token = "token";
    var uri = new Uri("https://speckle.xyz");
    var account = new Account()
    {
      token = token,
      serverInfo = new ServerInfo() { url = uri.AbsoluteUri },
    };

    var graphqlClientFactory = Create<IGraphQLClientFactory>();
    graphqlClientFactory
      .Setup(x => x.CreateGraphQLClient(account))
      .Returns(
        new GraphQLHttpClient(
          new GraphQLHttpClientOptions() { EndPoint = new(uri, "/graphql") },
          new NewtonsoftJsonSerializer(),
          httpClient
        )
      );

    using var client = new Client(
      Create<ILogger<Client>>(MockBehavior.Loose).Object,
      Create<ISdkActivityFactory>(MockBehavior.Loose).Object,
      graphqlClientFactory.Object,
      account
    );

    var x = await client.ExecuteGraphQLRequest<string>(new GraphQLRequest(), CancellationToken.None);
    x.Should().BeNull();
  }
}
