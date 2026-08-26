using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk.Api;
using Speckle.Sdk.Credentials;
using Xunit.Abstractions;
using Model = Speckle.Sdk.Bundles.Model;
using Version = Speckle.Sdk.Api.GraphQL.Models.Version;

namespace Speckle.Sdk.Tests.Integration.Bundles;

/// <summary>
/// Manual playground for <see cref="Operations.Receive3"/> against a real server. Set <see cref="MODEL_URL"/> and
/// <see cref="TOKEN"/> below (don't commit them), then run/debug this test. Left empty, the test returns immediately
/// (green in CI).
/// <code>
///   MODEL_URL: https://app.speckle.systems/projects/{projectId}/models/{modelId}[@{versionId}]   (no @ ⇒ latest)
/// </code>
/// </summary>
[Trait("Category", "Playground")]
public sealed class Receive3Playground(ITestOutputHelper output)
{
  // ── fill these in locally ─────────────────────────────────────────────────────────────────────────────
  private const string MODEL_URL = "";
  private const string TOKEN = "";

  [Fact]
  public async Task Receive3_FromModelUrl()
  {
    string modelUrl = MODEL_URL;
    string token = TOKEN;
    if (string.IsNullOrWhiteSpace(modelUrl) || string.IsNullOrWhiteSpace(token))
    {
      output.WriteLine("Skipped: set MODEL_URL and TOKEN at the top of Receive3Playground to run it.");
      return;
    }

    // ── 1. parse the model url ──────────────────────────────────────────────────────────────────────────
    var (server, projectId, modelId, versionId) = ParseModelUrl(modelUrl);
    output.WriteLine($"server={server} project={projectId} model={modelId} version={versionId ?? "(latest)"}");

    var services = new ServiceCollection();
    services.AddSpeckleSdk(new("Playground", "playground"), "v3");
    await using var provider = services.BuildServiceProvider();

    var account = new Account
    {
      token = token,
      serverInfo = new() { url = server.ToString() },
      userInfo = new(),
    };
    var client = provider.GetRequiredService<IClientFactory>().Create(account);

    // ── 2. resolve the version via GraphQL ──────────────────────────────────────────────────────────────
    Version version;
    if (versionId is null)
    {
      var model = await client.Model.GetWithVersions(modelId, projectId, versionsLimit: 1);
      version = model.versions.items.Single();
      versionId = version.id;
    }
    else
    {
      version = await client.Version.Get(versionId, projectId);
    }
    output.WriteLine(
      $"version {version.id} referencedObject={version.referencedObject} createdAt={version.createdAt:u}"
    );

    // ── 3. receive the bundle ───────────────────────────────────────────────────────────────────────────
    var operations = provider.GetRequiredService<IOperations>();
    using Model received = await operations.Receive3(
      server,
      projectId,
      modelId,
      versionId,
      token,
      null,
      CancellationToken.None
    );

    // ── 4. poke around — good place for a breakpoint ────────────────────────────────────────────────────
    output.WriteLine($"units={received.Units} objects={received.Objects.Count} files={received.Files.Count}");
    output.WriteLine($"bundle dir: {received.Directory}");
    foreach (var kv in received.Properties)
    {
      output.WriteLine($"  model.{kv.Key} = {kv.Value}");
    }

    foreach (var obj in received.Objects.Take(5))
    {
      output.WriteLine($"- {obj}  [{string.Join(" / ", obj.CollectionPath)}]");
      foreach (var kv in obj.Properties.Take(8))
      {
        output.WriteLine($"    {kv.Key} = {kv.Value}");
      }
      if (obj.TypeProperties.Count > 0)
      {
        output.WriteLine($"    (+{obj.TypeProperties.Count} type properties)");
      }
    }

    // Example query: every distinct property path in the model, with how many objects carry it.
    var pathCounts = received
      .Objects.SelectMany(o => o.Properties.Keys)
      .GroupBy(p => p)
      .OrderByDescending(g => g.Count())
      .Take(15);
    output.WriteLine("top property paths:");
    foreach (var g in pathCounts)
    {
      output.WriteLine($"    {g.Count(), 6}  {g.Key}");
    }

    Assert.NotEmpty(received.Objects);
  }

  /// <summary>Accepts <c>{server}/projects/{p}/models/{m}</c> with optional <c>@{versionId}</c> on the model id.</summary>
  private static (Uri Server, string ProjectId, string ModelId, string? VersionId) ParseModelUrl(string url)
  {
    var uri = new Uri(url);
    string[] segments = uri.AbsolutePath.Trim('/').Split('/');
    int p = Array.IndexOf(segments, "projects");
    int m = Array.IndexOf(segments, "models");
    if (p < 0 || m < 0 || p + 1 >= segments.Length || m + 1 >= segments.Length)
    {
      throw new ArgumentException(
        $"Not a model url: {url}. Expected .../projects/{{projectId}}/models/{{modelId}}[@versionId]"
      );
    }

    string modelPart = segments[m + 1];
    string? versionId = null;
    int at = modelPart.IndexOf('@');
    if (at >= 0)
    {
      versionId = modelPart[(at + 1)..];
      modelPart = modelPart[..at];
    }
    return (new Uri(uri.GetLeftPart(UriPartial.Authority)), segments[p + 1], modelPart, versionId);
  }
}
