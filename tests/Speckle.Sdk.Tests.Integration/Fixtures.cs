using System.Globalization;
using System.Net.Mime;
using System.Text;
using System.Web;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Common;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Tests.Integration.API.GraphQL.Resources;
using Speckle.Sdk.Transports;
using Version = Speckle.Sdk.Api.GraphQL.Models.Version;

[assembly: AssemblyTrait("Category", "Integration")]

namespace Speckle.Sdk.Tests.Integration;

public static class Fixtures
{
  public static readonly ServerInfo Server = new() { url = "http://localhost", name = "Docker Server" };

  public static IServiceProvider ServiceProvider { get; set; }

  static Fixtures()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly);
    ServiceProvider = TestServiceSetup.GetServiceProvider();
  }

  public static IClient Unauthed =>
    ServiceProvider
      .GetRequiredService<IClientFactory>()
      .Create(new Account { serverInfo = Server, userInfo = new UserInfo() });

  public static async Task<IClient> SeedUserWithClient()
  {
    return ServiceProvider.GetRequiredService<IClientFactory>().Create(await SeedUser());
  }

  public static async Task<Version> CreateVersion(IClient client, string projectId, string modelId)
  {
    using var remote = ServiceProvider.GetRequiredService<IServerTransportFactory>().Create(client.Account, projectId);
    var (objectId, _) = await ServiceProvider
      .GetRequiredService<IOperations>()
      .Send(new() { applicationId = "ASDF" }, remote, false);
    CreateVersionInput input = new(objectId, modelId, projectId);
    return await client.Version.Create(input);
  }

  public static async Task<Account> SeedUser()
  {
    var seed = Guid.NewGuid().ToString().ToLower();
    Dictionary<string, string> user = new()
    {
      ["email"] = $"{seed[..7]}@example.com",
      ["password"] = "12ABC3456789DEF0GHO",
      ["name"] = $"{seed[..5]} Name",
    };

    using var httpClient = new HttpClient(
      new HttpClientHandler { AllowAutoRedirect = false, CheckCertificateRevocationList = true }
    );

    httpClient.BaseAddress = new Uri(Server.url);

    string redirectUrl;
    try
    {
      var response = await httpClient.PostAsync(
        "/auth/local/register?challenge=challengingchallenge",
        // $"{Server.url}/auth/local/register?challenge=challengingchallenge",
        new StringContent(JsonConvert.SerializeObject(user), Encoding.UTF8, MediaTypeNames.Application.Json)
      );
      redirectUrl = response.Headers.Location!.AbsoluteUri;
    }
    catch (Exception e)
    {
      throw new Exception($"Cannot seed user on the server {Server.url}", e);
    }

    Uri uri = new(redirectUrl);
    var query = HttpUtility.ParseQueryString(uri.Query);

    string accessCode = query["access_code"] ?? throw new Exception("Redirect Uri has no 'access_code'.");
    Dictionary<string, string> tokenBody = new()
    {
      ["accessCode"] = accessCode,
      ["appId"] = "spklwebapp",
      ["appSecret"] = "spklwebapp",
      ["challenge"] = "challengingchallenge",
    };

    var tokenResponse = await httpClient.PostAsync(
      "/auth/token",
      new StringContent(JsonConvert.SerializeObject(tokenBody), Encoding.UTF8, MediaTypeNames.Application.Json)
    );
    var deserialised = JsonConvert.DeserializeObject<Dictionary<string, string>>(
      await tokenResponse.Content.ReadAsStringAsync()
    );

    var token = deserialised.NotNull()["token"].NotNull();

    return await ServiceProvider.GetRequiredService<IAccountFactory>().CreateAccount(new(Server.url), token);
  }

  public static Base GenerateSimpleObject()
  {
    var @base = new Base
    {
      ["foo"] = "foo",
      ["bar"] = "bar",
      ["baz"] = "baz",
      ["now"] = DateTime.Now.ToString(CultureInfo.InvariantCulture),
    };

    return @base;
  }

  public static Base GenerateNestedObject()
  {
    var @base = new Base
    {
      ["foo"] = "foo",
      ["bar"] = "bar",
      ["@baz"] = new Base() { ["mux"] = "mux", ["qux"] = "qux" },
    };

    return @base;
  }

  public static Blob[] GenerateThreeBlobs()
  {
    return new[] { GenerateBlob("blob 1 data"), GenerateBlob("blob 2 data"), GenerateBlob("blob 3 data") };
  }

  private static Blob GenerateBlob(string content)
  {
    var filePath = Path.GetTempFileName();
    File.WriteAllText(filePath, content);
    return new Blob(filePath);
  }

  [Obsolete(CommentResourceTests.SERVER_SKIP_MESSAGE)]
  internal static async Task<Comment> CreateComment(IClient client, string projectId, string modelId, string versionId)
  {
    var blobs = await SendBlobData(client.Account, projectId);
    var blobIds = blobs.Select(b => b.id.NotNull()).ToList();
    CreateCommentInput input = new(new(blobIds, null), projectId, $"{projectId},{modelId},{versionId}", null, null);
    return await client.Comment.Create(input);
  }

  internal static async Task<Blob[]> SendBlobData(Account account, string projectId)
  {
    using var remote = ServiceProvider.GetRequiredService<IServerTransportFactory>().Create(account, projectId);
    var blobs = Fixtures.GenerateThreeBlobs();
    Base myObject = new() { ["blobs"] = blobs };
    await ServiceProvider.GetRequiredService<IOperations>().Send(myObject, remote, false);
    return blobs;
  }
}

public class UserIdResponse
{
  public string userId { get; set; }
  public string apiToken { get; set; }
}
