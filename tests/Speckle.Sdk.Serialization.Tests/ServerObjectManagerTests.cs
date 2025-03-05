using System.Diagnostics.CodeAnalysis;
using System.Net;
using Moq;
using RichardSzalay.MockHttp;
using Speckle.Newtonsoft.Json;
using Speckle.Newtonsoft.Json.Linq;
using Speckle.Sdk.Common;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Serialisation.V2;
using Speckle.Sdk.Serialisation.V2.Send;

namespace Speckle.Sdk.Serialization.Tests;

[ExcludeFromCodeCoverage]
public abstract class MoqTest : IDisposable
{
  protected MoqTest() => Repository = new(MockBehavior.Strict);

  public void Dispose() => Repository.VerifyAll();

  protected MockRepository Repository { get; private set; } = new(MockBehavior.Strict);

  protected Mock<T> Create<T>(MockBehavior behavior = MockBehavior.Strict)
    where T : class => Repository.Create<T>(behavior);
}

public class ServerObjectManagerTests : MoqTest
{
  [Fact]
  public async Task DownloadObjects()
  {
    var id = Guid.Parse("6f422a35-6183-48b9-8021-d22ec97e8674").ToString();
    var id2 = Guid.Parse("ef2f7ea0-495a-46af-a9ad-f18a8a298597").ToString();
    var ct = new CancellationToken();
    var token = "token";
    var timeout = 2;
    var uri = new Uri("http://localhost");
    var streamId = "streamId";
    var jObject = new JObject { { "id", id }, { "value", true } };
    var jObject2 = new JObject { { "id", id2 }, { "value", true } };
    var mockHttp = new MockHttpMessageHandler();
    Dictionary<string, string> postParameters = new()
    {
      { "objects", JsonConvert.SerializeObject(new List<string> { id, id2 }) },
    };

    string serializedPayload = JsonConvert.SerializeObject(postParameters);
    mockHttp
      .When(HttpMethod.Post, $"http://localhost/api/getobjects/{streamId}")
      .WithContent(serializedPayload)
      .Respond(
        "application/json",
        $"{id}\t{jObject.ToString(Formatting.None)}\n{id2}\t{jObject2.ToString(Formatting.None)}\n"
      );
    var httpClient = mockHttp.ToHttpClient();
    var http = Create<ISpeckleHttp>();
    http.Setup(x => x.CreateHttpClient(It.IsAny<HttpClientHandler>(), timeout, token)).Returns(httpClient);

    var activityFactory = Create<ISdkActivityFactory>();
    activityFactory.Setup(x => x.Start(null, "DownloadObjects")).Returns((ISdkActivity?)null);

    var serverObjectManager = new ServerObjectManager(
      http.Object,
      activityFactory.Object,
      uri,
      streamId,
      token,
      new(timeout: TimeSpan.FromSeconds(timeout))
    );
    var results = serverObjectManager.DownloadObjects(new List<string> { id, id2 }, null, ct);
    var objects = new JObject();
    await foreach (var (x, json) in results)
    {
      objects.Add(x, JToken.Parse(json));
    }

    await VerifyJson(objects.ToString(Formatting.Indented));
  }

  [Fact]
  public async Task DownloadSingleObject()
  {
    var id = Guid.Parse("6f422a35-6183-48b9-8021-d22ec97e8674").ToString();
    var ct = new CancellationToken();
    var token = "token";
    var timeout = 2;
    var uri = new Uri("http://localhost");
    var streamId = "streamId";
    var jObject = new JObject { { "id", id }, { "value", true } };
    var mockHttp = new MockHttpMessageHandler();
    mockHttp
      .When(HttpMethod.Get, $"http://localhost/objects/{streamId}/{id}/single")
      .Respond("application/json", $"{jObject.ToString(Formatting.None)}\n");
    var httpClient = mockHttp.ToHttpClient();
    var http = Create<ISpeckleHttp>();
    http.Setup(x => x.CreateHttpClient(It.IsAny<HttpClientHandler>(), timeout, token)).Returns(httpClient);

    var activityFactory = Create<ISdkActivityFactory>();
    activityFactory.Setup(x => x.Start(null, "DownloadSingleObject")).Returns((ISdkActivity?)null);

    var serverObjectManager = new ServerObjectManager(
      http.Object,
      activityFactory.Object,
      uri,
      streamId,
      token,
      new(timeout: TimeSpan.FromSeconds(timeout))
    );
    var json = await serverObjectManager.DownloadSingleObject(id, null, ct);
    await VerifyJson(json);
  }

  [Fact]
  public async Task HasObjects()
  {
    var id = Guid.Parse("6f422a35-6183-48b9-8021-d22ec97e8674").ToString();
    var id2 = Guid.Parse("ef2f7ea0-495a-46af-a9ad-f18a8a298597").ToString();
    var ct = new CancellationToken();
    var token = "token";
    var timeout = 2;
    var uri = new Uri("http://localhost");
    var streamId = "streamId";
    var mockHttp = new MockHttpMessageHandler();
    Dictionary<string, string> postParameters = new()
    {
      { "objects", JsonConvert.SerializeObject(new List<string> { id, id2 }) },
    };

    Dictionary<string, bool> responseParameters = new() { { id, true }, { id2, false } };
    string serializedPayload = JsonConvert.SerializeObject(postParameters);
    mockHttp
      .When(HttpMethod.Post, $"http://localhost/api/diff/{streamId}")
      .WithContent(serializedPayload)
      .Respond("application/json", JsonConvert.SerializeObject(responseParameters));
    var httpClient = mockHttp.ToHttpClient();
    var http = Create<ISpeckleHttp>();
    http.Setup(x => x.CreateHttpClient(It.IsAny<HttpClientHandler>(), timeout, token)).Returns(httpClient);

    var activityFactory = Create<ISdkActivityFactory>();
    activityFactory.Setup(x => x.Start(null, "HasObjects")).Returns((ISdkActivity?)null);

    var serverObjectManager = new ServerObjectManager(
      http.Object,
      activityFactory.Object,
      uri,
      streamId,
      token,
      new(timeout: TimeSpan.FromSeconds(timeout))
    );
    var results = await serverObjectManager.HasObjects(new List<string> { id, id2 }, ct);

    await Verify(results);
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public async Task UploadObjects(bool compressed)
  {
    var id = Guid.Parse("6f422a35-6183-48b9-8021-d22ec97e8674").ToString();
    var id2 = Guid.Parse("ef2f7ea0-495a-46af-a9ad-f18a8a298597").ToString();
    var ct = new CancellationToken();
    var token = "token";
    var timeout = 2;
    var uri = new Uri("http://localhost");
    var streamId = "streamId";
    var jObject = new JObject { { "id", id }, { "value", true } };
    var jObject2 = new JObject { { "id", id2 }, { "value", true } };

    var mockHttp = new MockHttpMessageHandler();
    mockHttp
      .When(HttpMethod.Post, $"http://localhost/objects/{streamId}")
      .Respond(x => HandleUploadRequest(x, compressed));
    var httpClient = mockHttp.ToHttpClient();
    var http = Create<ISpeckleHttp>();
    http.Setup(x => x.CreateHttpClient(It.IsAny<HttpClientHandler>(), timeout, token)).Returns(httpClient);

    var activityFactory = Create<ISdkActivityFactory>();
    activityFactory.Setup(x => x.Start(null, "UploadObjects")).Returns((ISdkActivity?)null);

    var serverObjectManager = new ServerObjectManager(
      http.Object,
      activityFactory.Object,
      uri,
      streamId,
      token,
      new(timeout: TimeSpan.FromSeconds(timeout), boundary: "boundary")
    );
    await serverObjectManager.UploadObjects(
      new List<BaseItem>
      {
        new(new(id), new Json(jObject.ToString()), true, null),
        new(new(id2), new Json(jObject2.ToString()), true, null),
      },
      compressed,
      null,
      ct
    );
  }

  private async Task<HttpResponseMessage> HandleUploadRequest(HttpRequestMessage req, bool compressed)
  {
    var content = await req.Content.NotNull().ReadAsStringAsync().ConfigureAwait(false);
    await Verify(content).UseParameters(compressed);
    return new HttpResponseMessage(HttpStatusCode.OK);
  }
}
