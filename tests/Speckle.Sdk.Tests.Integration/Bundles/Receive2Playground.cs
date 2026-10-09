using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk.Api;
using Speckle.Sdk.Bundles;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;
using Speckle.Sdk.Pipelines.Receive.Artifacts;
using Xunit.Abstractions;

namespace Speckle.Sdk.Tests.Integration.Bundles;

/// <summary>
/// Manual playground for the legacy <see cref="Operations.Receive2"/> surface against a real server — the path an
/// old script takes. Point it at any vintage and compare what comes back:
/// <list type="bullet">
///   <item>a v3 version (hash in <c>referencedObject</c>) → the legacy object graph, downloaded as-is</item>
///   <item>a migrated bundle (server converted a v2/v3 graph; <c>bundle.…</c> reference) → the Base projection</item>
///   <item>a natively published bundle → the Base projection</item>
/// </list>
/// Set <see cref="MODEL_URL"/> and <see cref="TOKEN"/> below (don't commit them), then run/debug. Left empty, the
/// test returns immediately (green in CI).
/// <code>
///   MODEL_URL: https://app.speckle.systems/projects/{projectId}/models/{modelId}[@{versionId}]   (no @ ⇒ latest)
/// </code>
/// </summary>
[Trait("Category", "Playground")]
public sealed class Receive2Playground(ITestOutputHelper output)
{
  // ── fill these in locally ─────────────────────────────────────────────────────────────────────────────
  private const string MODEL_URL = "";
  private const string TOKEN = "";

  [Fact]
  public async Task Receive2_FromModelUrl()
  {
    if (string.IsNullOrWhiteSpace(MODEL_URL) || string.IsNullOrWhiteSpace(TOKEN))
    {
      output.WriteLine("Skipped: set MODEL_URL and TOKEN at the top of Receive2Playground to run it.");
      return;
    }

    var services = new ServiceCollection();
    services.AddSpeckleSdk(new("Playground", "playground"), "v3");
    await using var provider = services.BuildServiceProvider();

    var url = ModelUrl.Parse(MODEL_URL);
    var account = new Account
    {
      token = TOKEN,
      serverInfo = new() { url = url.Server.ToString() },
      userInfo = new(),
    };

    // ── resolve the version + its referencedObject (what Receive2 dispatches on) ────────────────────────
    using var client = provider.GetRequiredService<IClientFactory>().Create(account);
    string versionId;
    string? referencedObject;
    if (url.VersionId is { } pinned)
    {
      var version = await client.Version.Get(pinned, url.ProjectId);
      versionId = version.id;
      referencedObject = version.referencedObject;
    }
    else
    {
      var model = await client.Model.GetWithVersions(url.ModelId, url.ProjectId, versionsLimit: 1);
      var latest = model.versions.items[0];
      versionId = latest.id;
      referencedObject = latest.referencedObject;
    }
    bool isBundle = BundleReference.IsBundleReference(referencedObject);
    output.WriteLine($"version={versionId}  referencedObject={referencedObject}");
    output.WriteLine(
      isBundle
        ? "→ bundle reference: Receive2 projects the bundle to a Base tree"
        : "→ object hash: Receive2 downloads the legacy graph"
    );

    // ── the legacy receive (obsolete on purpose — this playground exercises exactly that surface) ───────
#pragma warning disable CS0618
    Base root = await provider
      .GetRequiredService<IOperations>()
      .Receive2(url.Server, url.ProjectId, referencedObject!, TOKEN, null, CancellationToken.None);
#pragma warning restore CS0618

    // ── what an old script sees — good place for a breakpoint ───────────────────────────────────────────
    output.WriteLine($"root: {root.speckle_type}  units={root["units"]}  version-marker={root["version"] ?? "(none)"}");
    output.WriteLine($"root members: {string.Join(", ", root.GetMembers(DynamicBaseMemberType.All).Keys.Take(12))}");

    var byType = new Dictionary<string, int>();
    int total = 0;
    void Walk(Base b)
    {
      total++;
      byType[b.speckle_type] = byType.GetValueOrDefault(b.speckle_type) + 1;
      if (b is Collection c)
      {
        foreach (var child in c.elements)
        {
          Walk(child);
        }
      }
    }
    Walk(root);
    output.WriteLine($"tree objects (via Collection.elements): {total}");
    foreach (var kv in byType.OrderByDescending(kv => kv.Value).Take(10))
    {
      output.WriteLine($"    {kv.Value, 6}  {kv.Key}");
    }

    Assert.NotNull(root);
  }
}
