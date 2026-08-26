using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk.Api;
using Xunit.Abstractions;
using Model = Speckle.Sdk.Bundles.Model;

namespace Speckle.Sdk.Tests.Integration.Bundles;

/// <summary>
/// Manual playground for <see cref="Operations.Receive3(string, string?, Speckle.Sdk.Bundles.ReceiveOptions?, CancellationToken)"/> against a real server. Set <see cref="MODEL_URL"/> and
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

    var services = new ServiceCollection();
    services.AddSpeckleSdk(new("Playground", "playground"), "v3");
    await using var provider = services.BuildServiceProvider();

    // ── receive by url (latest version unless the url pins one with @versionId) ──────────────────────────
    var operations = provider.GetRequiredService<IOperations>();
    using Model received = await operations.Receive3(modelUrl, token, options: null, CancellationToken.None);
    output.WriteLine($"project={received.ProjectId} model={received.ModelId} version={received.VersionId}");

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
}
