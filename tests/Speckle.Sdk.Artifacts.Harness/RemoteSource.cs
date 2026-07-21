using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.V2;
using Speckle.Sdk.Serialisation.V2.Receive;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Artifacts.Harness;

/// <summary>
/// Pulls a model's object graph directly from a remote Speckle server via the SDK's
/// server-backed deserialize process (no local file, no local account store — the token
/// is passed explicitly). Registered in the DI container; the deserialize-process factory
/// is injected via the constructor rather than resolved from a passed-in service provider.
/// </summary>
public sealed class RemoteSource(IDeserializeProcessFactory deserializeProcessFactory)
{
  /// <summary>
  /// Resolves the latest version of a model (its id + referencedObject/rootId) via GraphQL.
  /// Mirrors: project(id) -> model(id) -> versions(limit:1){ items { id referencedObject } }.
  /// </summary>
  public async Task<(string versionId, string rootId)> ResolveLatestVersionAsync(
    string serverUrl,
    string projectId,
    string modelId,
    string token,
    CancellationToken ct
  )
  {
    const string query =
      @"query LatestVersion($projectId: String!, $modelId: String!) {
  project(id: $projectId) {
    model(id: $modelId) {
      versions(limit: 1) {
        items { id referencedObject }
      }
    }
  }
}";

    var payload = new { query, variables = new { projectId, modelId } };

    using var http = new HttpClient();
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    // app.speckle.systems is behind Cloudflare, which 403s (code 1010) requests with no User-Agent.
    http.DefaultRequestHeaders.UserAgent.ParseAdd("speckle-backfill-validation/1.0");

    var graphqlUrl = serverUrl.TrimEnd('/') + "/graphql";
    using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    using var resp = await http.PostAsync(graphqlUrl, content, ct).ConfigureAwait(false);
    var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    if (!resp.IsSuccessStatusCode)
    {
      throw new InvalidOperationException(
        $"GraphQL latest-version query failed ({(int)resp.StatusCode} {resp.StatusCode}): {body}"
      );
    }

    using var doc = JsonDocument.Parse(body);
    if (doc.RootElement.TryGetProperty("errors", out var errors))
    {
      throw new InvalidOperationException($"GraphQL returned errors: {errors}");
    }

    var items = doc
      .RootElement.GetProperty("data")
      .GetProperty("project")
      .GetProperty("model")
      .GetProperty("versions")
      .GetProperty("items");

    if (items.GetArrayLength() == 0)
    {
      throw new InvalidOperationException($"Model {modelId} in project {projectId} has no versions.");
    }

    var first = items[0];
    var versionId = first.GetProperty("id").GetString()!;
    var rootId = first.GetProperty("referencedObject").GetString()!;
    return (versionId, rootId);
  }

  /// <summary>
  /// Deserializes a root object id into a <see cref="Base"/> graph using the SDK's
  /// server-backed deserialize process, via the injected <see cref="IDeserializeProcessFactory"/>.
  /// Follows the exact pattern in tests/Speckle.Sdk.Serialization.Testing/Program.cs.
  /// </summary>
  public async Task<Base> DeserializeFromServerAsync(
    string serverUrl,
    string projectId,
    string rootId,
    string token,
    CancellationToken ct
  )
  {
    var progress = new ConsoleProgress();

    var process = deserializeProcessFactory.CreateDeserializeProcess(
      new Uri(serverUrl),
      projectId,
      token,
      progress,
      ct,
      new DeserializeProcessOptions(SkipCache: false)
    );
    await using (process.ConfigureAwait(false))
    {
      return await process.Deserialize(rootId).ConfigureAwait(false);
    }
  }

  /// <summary>Minimal debounced progress writer (the SDK testing project's Progress is not referenced here).</summary>
  private sealed class ConsoleProgress : IProgress<ProgressArgs>
  {
    private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(1);
    private DateTime _last = DateTime.UtcNow;
    private long _bytes;

    public void Report(ProgressArgs value)
    {
      if (value.ProgressEvent == ProgressEvent.DownloadBytes)
      {
        Interlocked.Add(ref _bytes, value.Count);
      }
      var now = DateTime.UtcNow;
      if (now - _last < Debounce)
      {
        return;
      }
      _last = now;
      if (value.ProgressEvent == ProgressEvent.DownloadBytes)
      {
        Console.WriteLine($"  download bytes: {_bytes:N0}");
      }
      else
      {
        Console.WriteLine($"  {value.ProgressEvent}: {value.Count}/{value.Total}");
      }
    }
  }
}
