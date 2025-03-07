using System.Net;
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
    Assert.NotNull(response);
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
    await Assert.ThrowsAsync<HttpRequestException>(
      async () => await speckleHttp.HttpPing(uri, mockHttp.ToHttpClient())
    );
  }
}
