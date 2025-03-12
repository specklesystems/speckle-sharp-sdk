using FluentAssertions;
using GraphQL;
using Microsoft.Extensions.Logging;
using Moq;
using RichardSzalay.MockHttp;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Helpers;
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

    var speckleHttp = Create<ISpeckleHttp>();
    speckleHttp.Setup(x => x.CreateHttpClient(null, 30, token)).Returns(httpClient);

    var application = Create<ISpeckleApplication>();
    application.Setup(x => x.ApplicationAndVersion).Returns("test");

    using var client = new Client(
      Create<ILogger<Client>>(MockBehavior.Loose).Object,
      Create<ISdkActivityFactory>(MockBehavior.Loose).Object,
      application.Object,
      speckleHttp.Object,
      new Account()
      {
        token = token,
        serverInfo = new ServerInfo() { url = uri.AbsoluteUri },
      }
    );

    var x = await client.ExecuteGraphQLRequest<string>(new GraphQLRequest(), CancellationToken.None);
    x.Should().BeNull();
  }
}
