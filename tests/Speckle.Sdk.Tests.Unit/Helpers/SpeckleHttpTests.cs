using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RichardSzalay.MockHttp;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Testing;

namespace Speckle.Sdk.Tests.Unit.Helpers;

public class SpeckleHttpTests : MoqTest
{
  [Fact]
  public async Task HttpPing()
  {
    using var mockHttp = new MockHttpMessageHandler();
    var speckleHttp = new SpeckleHttp(
      Create<ILogger<SpeckleHttp>>(MockBehavior.Loose).Object,
      Create<ISpeckleHttpClientHandlerFactory>().Object
    );

    var uri = new Uri("https://speckle.xyz");
    mockHttp.When(uri.AbsoluteUri).Respond("application/json", "{}");
    var response = await speckleHttp.HttpPing(uri, mockHttp.ToHttpClient());
    response.Should().NotBeNull();
  }

  [Fact]
  public async Task HttpPing_Failed()
  {
    using var mockHttp = new MockHttpMessageHandler();
    var speckleHttp = new SpeckleHttp(
      Create<ILogger<SpeckleHttp>>(MockBehavior.Loose).Object,
      Create<ISpeckleHttpClientHandlerFactory>().Object
    );

    var uri = new Uri("https://speckle.xyz");
    mockHttp.When(uri.AbsoluteUri).Respond(HttpStatusCode.Unauthorized);
    await Assert.ThrowsAsync<HttpRequestException>(async () =>
      await speckleHttp.HttpPing(uri, mockHttp.ToHttpClient())
    );
  }

  [Fact]
  public void CreateHttpClient_NoToken()
  {
    var clientHandlerFactory = Create<ISpeckleHttpClientHandlerFactory>();

    using var mockHttp1 = new MockHttpMessageHandler();
    using var mockHttp2 = new MockHttpMessageHandler();
    var speckleHandler = Create<DelegatingHandler>();

    clientHandlerFactory
      .Setup(x => x.Create(mockHttp1, SpeckleHttp.DEFAULT_TIMEOUT_SECONDS))
      .Returns(speckleHandler.Object);

    var speckleHttp = new SpeckleHttp(
      Create<ILogger<SpeckleHttp>>(MockBehavior.Loose).Object,
      clientHandlerFactory.Object
    );

    var client = speckleHttp.CreateHttpClient(mockHttp1);
    client.Should().NotBeNull();
    client.DefaultRequestHeaders.Should().BeEmpty();
  }
}
