using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Speckle.Sdk.Artifacts.Harness.Transports;
using Speckle.Sdk.Common;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Models;
using Speckle.Sdk.Pipelines.Progress;
using Speckle.Sdk.Serialisation;

namespace Speckle.Sdk.Artifacts.Harness;

/// <summary>
/// End-to-end artefact-bundle harness: resolves an object graph (NDJSON dump, DuckDB packfile, or a
/// remote server), produces the parquet bundle on disk, and optionally uploads it via the v2
/// envelope-bundle flow. Registered in the DI container; its typed entry points are invoked
/// by the <see cref="HarnessCommandLine"/> command actions.
/// </summary>
internal sealed class Harness(
  RemoteSource remoteSource,
  GraphArtifactProducerFactory producerFactory,
  BundleUploader bundleUploader,
  ISdkActivityFactory activityFactory,
  ILogger<Harness> logger
)
{
  /// <summary>Loads a graph from an NDJSON dump and produces (and optionally uploads) the bundle.</summary>
  public async Task<int> RunNdjson(FileInfo ndjson, string root, string? outDir, string[]? upload, CancellationToken ct)
  {
    var (localRoot, baseName) = await LoadNdjson(ndjson, root).ConfigureAwait(false);
    if (localRoot is null)
    {
      return 1;
    }
    return await ProduceAndUpload(localRoot, baseName, outDir, upload, ct).ConfigureAwait(false);
  }

  /// <summary>Loads a graph from a DuckDB packfile on disk and produces (and optionally uploads) the bundle.</summary>
  public async Task<int> RunPackfile(
    FileInfo packfile,
    string? root,
    string? outDir,
    string[]? upload,
    CancellationToken ct
  )
  {
    logger.LogInformation("Input: {InputPath}", packfile);
    var graph = await LoadPackfileGraph(packfile, root).ConfigureAwait(false);
    if (graph is null)
    {
      return 1;
    }
    return await ProduceAndUpload(graph, packfile.Name, outDir, upload, ct).ConfigureAwait(false);
  }

  /// <summary>Migrates a version from a source server to a destination (each part defaulting to the source),
  /// always uploading the produced bundle. Fetches the source graph by downloading its DuckDB packfile
  /// (default) or via the legacy REST deserialize (<paramref name="legacyApi"/>).</summary>
  public async Task<int> RunRemote(
    Uri server,
    string project,
    string model,
    string? version,
    Uri? destServer,
    string? destProject,
    string? destModel,
    bool legacyApi,
    string? outDir,
    CancellationToken ct
  )
  {
    var token =
      Environment.GetEnvironmentVariable("SPECKLE_SRC_TOKEN")
      ?? Environment
        .GetEnvironmentVariable("SPECKLE_TOKEN")
        .NotNull("Expected SPECKLE_TOKEN or SPECKLE_SRC_TOKEN to be set");

    Base? graph = legacyApi
      ? await LoadFromServerLegacy(server, project, model, version, token, ct).ConfigureAwait(false)
      : await LoadFromServerPackfile(server, project, model, version, token, ct).ConfigureAwait(false);
    if (graph is null)
    {
      return 1;
    }

    string[] destination = [(destServer ?? server).AbsoluteUri, destProject ?? project, destModel ?? model];
    return await ProduceAndUpload(graph, model, outDir, destination, ct).ConfigureAwait(false);
  }

  // Legacy remote fetch: resolve the rootId via GraphQL, then deserialize over the SDK's server-backed process.
  private async Task<Base> LoadFromServerLegacy(
    Uri server,
    string project,
    string model,
    string? version,
    string token,
    CancellationToken ct
  )
  {
    string rootId;
    if (version is not null)
    {
      logger.LogInformation("Resolving rootId for version {VersionId} …", version);
      (_, rootId) = await ResolveVersionRootId(server, project, model, version, token).ConfigureAwait(false);
    }
    else
    {
      logger.LogInformation("Resolving latest version of {ProjectId}/{ModelId} …", project, model);
      (version, rootId) = await remoteSource
        .ResolveLatestVersionAsync(server, project, model, token, ct)
        .ConfigureAwait(false);
      logger.LogInformation("Latest version {VersionId} → rootId {RootId}", version, rootId);
    }

    logger.LogInformation("Deserializing from server …");
    return await remoteSource.DeserializeFromServerAsync(server, project, rootId, token, ct).ConfigureAwait(false);
  }

  // Default remote fetch: download the version's DuckDB packfile to a temp file, then read it like the
  // on-disk packfile path.
  private async Task<Base?> LoadFromServerPackfile(
    Uri server,
    string project,
    string model,
    string? version,
    string token,
    CancellationToken ct
  )
  {
    if (version is null)
    {
      logger.LogInformation("Resolving latest version of {ProjectId}/{ModelId} …", project, model);
      (version, _) = await remoteSource
        .ResolveLatestVersionAsync(server, project, model, token, ct)
        .ConfigureAwait(false);
      logger.LogInformation("Latest version {VersionId}", version);
    }

    var packfile = new FileInfo(Path.Combine(Path.GetTempPath(), $"speckle-src-{version}.duckdb"));
    try
    {
      using var http = new HttpClient { BaseAddress = server };
      http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
      // Cloudflare (app.speckle.systems) 403s requests with no User-Agent.
      http.DefaultRequestHeaders.UserAgent.ParseAdd("speckle-artefact-harness/1.0");

      logger.LogInformation("Downloading packfile for version {VersionId} …", version);
      await DuckDbHelper
        .DownloadDuckFile(http, project, model, version, packfile, new Progress<StreamProgressArgs>(), ct)
        .ConfigureAwait(false);
      logger.LogInformation("Downloaded {Bytes} bytes", packfile.Length);

      return await LoadPackfileGraph(packfile, null).ConfigureAwait(false);
    }
    finally
    {
      packfile.Refresh();
      if (packfile.Exists)
      {
        packfile.Delete();
      }
    }
  }

  // Reads a DuckDB packfile into a Base graph. The transport stays alive for the whole deserialize (children
  // are fetched from it by id).
  private async Task<Base?> LoadPackfileGraph(FileInfo packfile, string? root)
  {
    using var transport = new DuckDbTransport(new PackFileManager(packfile, activityFactory));
    var rootId = root ?? transport.GetRootObjectId();
    var rootJson = await transport.GetObject(rootId).ConfigureAwait(false);
    if (rootJson is null)
    {
      logger.LogError("Root '{RootId}' not found in packfile.", rootId);
      return null;
    }
    logger.LogInformation("Root: {RootId}", rootId);

    var deserializer = new SpeckleObjectDeserializer { ReadTransport = transport };
    return await deserializer.DeserializeAsync(rootJson).ConfigureAwait(false);
  }

  // Produce the bundle on disk from a resolved graph, then optionally upload it (when `upload` — the
  // {serverUrl, projectId, modelId} triple — is supplied).
  private async Task<int> ProduceAndUpload(
    Base root,
    string baseName,
    string? outDir,
    string[]? upload,
    CancellationToken ct
  )
  {
    logger.LogInformation("Deserialized root [{SpeckleType}] id={RootId}", root.speckle_type, root.id);

    outDir ??= Path.Combine(Path.GetTempPath(), $"speckle-artefact-{baseName}-{DateTime.UtcNow:yyyyMMddHHmmss}");
    logger.LogInformation("Output: {OutDir} (base {BaseName})", outDir, baseName);

    GraphArtifactProducer.Stats stats;
    using (var producer = producerFactory.Create(outDir, baseName))
    {
      stats = producer.Produce(root);
    }

    logger.LogInformation("Produce stats:\n{Stats}", stats);
    foreach (var note in stats.Notes)
    {
      logger.LogInformation("Note: {Note}", note);
    }
    foreach (var f in Directory.GetFiles(outDir).OrderBy(x => x))
    {
      logger.LogInformation("Bundle file {FileName} {Bytes} bytes", Path.GetFileName(f), new FileInfo(f).Length);
    }

    // An absent multi-value option parses to an empty array (not null), so guard on length.
    if (upload is null || upload.Length == 0)
    {
      return 0;
    }

    if (root.id is not { } rootObjectId)
    {
      logger.LogError(
        "Cannot upload — root.id is null (a locally-built graph that was not deserialised from a hashed source has no id)."
      );
      return 4;
    }

    var dstToken =
      Environment.GetEnvironmentVariable("SPECKLE_DST_TOKEN")
      ?? Environment
        .GetEnvironmentVariable("SPECKLE_TOKEN")
        .NotNull("Expected either SPECKLE_DST_TOKEN or SPECKLE_TOKEN to be set");

    Uri dstServer = new(upload[0]);
    var dstProject = upload[1];
    var dstModel = upload[2];
    // totalChildrenCount: best-effort = (object count - 1) for the root. Server stores it on
    // the commit; not load-bearing for serving. See README "uncertainties".
    int? totalChildrenCount = stats.Objects > 0 ? Math.Max(0, stats.Objects - 1) : null;
    logger.LogInformation("Uploading (v2 envelope bundle) …");
    var result = await bundleUploader
      .UploadAsync(dstServer, dstProject, dstModel, outDir, rootObjectId, totalChildrenCount, dstToken, ct)
      .ConfigureAwait(false);

    Uri viewerUrl = new(dstServer, $"/projects/{dstProject}/models/{dstModel}@{result.VersionId}");
    logger.LogInformation("Upload OK versionId={VersionId} files={FileCount}", result.VersionId, result.Files.Count);
    logger.LogInformation("Viewer: {ViewerUrl}", viewerUrl);
    return 0;
  }

  // Resolve a specific version's referencedObject (rootId) via GraphQL.
  private static async Task<(string versionId, string rootId)> ResolveVersionRootId(
    Uri serverUrl,
    string projectId,
    string modelId,
    string versionId,
    string token
  )
  {
    // Reuse the latest-version resolver if no pin; otherwise query the single version.
    using var http = new System.Net.Http.HttpClient();
    http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    const string QUERY = """
      query Version($projectId: String!, $modelId: String!, $versionId: String!) {
        project(id: $projectId) {
          model(id: $modelId) {
            version(id: $versionId) { id referencedObject }
          }
        }
      }
      """;
    var payload = System.Text.Json.JsonSerializer.Serialize(
      new
      {
        query = QUERY,
        variables = new
        {
          projectId,
          modelId,
          versionId,
        },
      }
    );
    using var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
    using var resp = await http.PostAsync(new Uri(serverUrl, "/graphql"), content).ConfigureAwait(false);
    var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
    if (!resp.IsSuccessStatusCode)
    {
      throw new InvalidOperationException($"version query failed ({(int)resp.StatusCode}): {body}");
    }
    using var doc = System.Text.Json.JsonDocument.Parse(body);
    if (doc.RootElement.TryGetProperty("errors", out var errors))
    {
      throw new InvalidOperationException($"version query GraphQL errors: {errors}");
    }
    var v = doc.RootElement.GetProperty("data").GetProperty("project").GetProperty("model").GetProperty("version");
    return (v.GetProperty("id").GetString()!, v.GetProperty("referencedObject").GetString()!);
  }

  // ── local ndjson → Base graph (existing behaviour) ──────────────────────────────────────
  private async Task<(Base? root, string baseName)> LoadNdjson(FileInfo ndjson, string rootOption)
  {
    var baseName = ndjson.Name;
    logger.LogInformation("Input: {InputPath}", ndjson);

    var transport = new NDJsonTransport();
    var count = transport.Initialize(ndjson);
    logger.LogInformation("Loaded {LineCount} objects into transport", count);

    var rootId = rootOption == "auto" ? transport.DetectRoot() : rootOption;
    var rootJson = rootId is null ? null : await transport.GetObject(rootId).ConfigureAwait(false);
    if (rootJson is null)
    {
      logger.LogError("Root '{RootId}' not found. Available collection-like candidates:", rootId);
      foreach (var c in transport.CollectionCandidates().Take(10))
      {
        logger.LogInformation("Candidate {Candidate}", c);
      }
      return (null, baseName);
    }
    logger.LogInformation("Root: {RootId}", rootId);

    var deserializer = new SpeckleObjectDeserializer { ReadTransport = transport };
    var root = await deserializer.DeserializeAsync(rootJson).ConfigureAwait(false);
    return (root, baseName);
  }
}
