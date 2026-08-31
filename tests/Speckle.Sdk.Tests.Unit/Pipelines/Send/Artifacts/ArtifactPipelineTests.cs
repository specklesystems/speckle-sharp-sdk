using AwesomeAssertions;
using RichardSzalay.MockHttp;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Pipelines.Send.Artifacts;
using Speckle.Sdk.Testing;

namespace Speckle.Sdk.Tests.Unit.Pipelines.Send.Artifacts;

public class ArtifactPipelineTests : MoqTest
{
  // ENG-9490: the server reads clientAppVersion for envelope-born versions from the
  // apollographql-client-version header of the v2 uploads/complete request; pin that every
  // Speckle-bound request of the upload rail carries the apollo client headers.
  [Fact]
  public async Task SpeckleBoundRequests_CarryApolloClientHeaders()
  {
    var expectedVersion = typeof(ArtifactPipeline).Assembly.GetName().Version?.ToString();
    expectedVersion.Should().NotBeNullOrEmpty();

    using var mockHttp = new MockHttpMessageHandler();
    mockHttp
      .Expect(HttpMethod.Post, "https://example.com/api/v2/projects/proj/modelingestion/ing/uploads/sign")
      .WithHeaders("apollographql-client-name", "TestHost 1.2.3")
      .WithHeaders("apollographql-client-version", expectedVersion!)
      .Respond("application/json", """{"uploads":{}}""");
    mockHttp
      .Expect(HttpMethod.Post, "https://example.com/api/v2/projects/proj/modelingestion/ing/uploads/complete")
      .WithHeaders("apollographql-client-name", "TestHost 1.2.3")
      .WithHeaders("apollographql-client-version", expectedVersion!)
      .Respond("application/json", """{"versionId":"ver"}""");

    var speckleHttp = Create<ISpeckleHttp>();
    speckleHttp
      .Setup(x => x.CreateHttpClient(null, SpeckleHttp.DEFAULT_TIMEOUT_SECONDS, "token"))
      .Returns(mockHttp.ToHttpClient());
    speckleHttp
      .Setup(x => x.CreateHttpClient(null, SpeckleHttp.DEFAULT_TIMEOUT_SECONDS, null))
      .Returns(new HttpClient());

    var application = new SpeckleApplication
    {
      HostApplication = "TestHost",
      HostApplicationVersion = "1.2.3",
      Slug = "testhost",
      SpeckleVersion = "3.0.0",
    };
    var account = new Account
    {
      token = "token",
      serverInfo = new() { url = "https://example.com" },
    };

    var factory = new ArtifactPipelineFactory(application, speckleHttp.Object, new NullActivityFactory());
    using var pipeline = factory.CreateInstance("proj", "ing", "ver", account, "out", CancellationToken.None);

    var versionId = await pipeline.UploadFilesAsync(new Dictionary<string, string>(), "root", 0);

    versionId.Should().Be("ver");
    mockHttp.VerifyNoOutstandingExpectation();
  }
}
