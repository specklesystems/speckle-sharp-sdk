using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Speckle.Sdk.Artifacts.Harness.Migration;
using Speckle.Sdk.Artifacts.Harness.Transports;
using Speckle.Sdk.Common;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Models;
using Speckle.Sdk.Pipelines.Progress;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Serialisation.Utilities;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Artifacts.Harness;

/// <summary>
/// End-to-end artefact-bundle harness: resolves an object graph (a DuckDB packfile, on disk or fetched
/// from a server), produces the parquet bundle on disk, and uploads it either ONTO THE SOURCE VERSION
/// (in place, via the bundle-migration API) or as a NEW version on a destination (the v2 envelope-bundle
/// flow). Registered in the DI container; its typed entry points are invoked by the
/// <see cref="HarnessCommandLine"/> command actions.
/// </summary>
internal sealed class Harness(
  RemoteSource remoteSource,
  GraphArtifactProducerFactory producerFactory,
  BundleUploader bundleUploader,
  BundleMigrationClient migrationClient,
  ISdkActivityFactory activityFactory,
  ILogger<Harness> logger,
  IServerTransportFactory serverTransportFactory
)
{
  /// <summary>Migrates a server version. With no destination the bundle is uploaded ONTO THAT VERSION
  /// (in place); with a destination it is uploaded as a NEW version there. Fetches the source graph by
  /// downloading its DuckDB packfile (default) or via the legacy REST deserialize
  /// (<paramref name="legacyApi"/>, new-version flow only).</summary>
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
    // SPIKE(ticket-17, local-only): unset/empty token ⇒ anonymous — public-project reads work only
    // with the Authorization header fully absent (server 403s empty/invalid bearers).
    var authToken =
      Environment.GetEnvironmentVariable("SPECKLE_SRC_TOKEN") ?? Environment.GetEnvironmentVariable("SPECKLE_TOKEN") ?? "";

    // No destination → migrate the source version in place. The CLI guarantees a version is supplied here.
    if (destServer is null && destProject is null && destModel is null)
    {
      return await RunInPlace(server, project, model, version.NotNull(), authToken, outDir, ct).ConfigureAwait(false);
    }

    Base? graph = legacyApi
      ? await LoadFromServerLegacy(server, project, model, version, authToken, ct).ConfigureAwait(false)
      : await LoadFromServerPackfile(server, project, model, version, authToken, ct).ConfigureAwait(false);
    if (graph is null)
    {
      return 1;
    }

    // SPIKE(ticket-17, local-only): HARNESS_NO_UPLOAD=1 stops after Produce (fixture manufacture).
    string[]? destination =
      Environment.GetEnvironmentVariable("HARNESS_NO_UPLOAD") == "1"
        ? null
        : [(destServer ?? server).AbsoluteUri, destProject ?? project, destModel ?? model];
    return await ProduceAndUpload(graph, model, outDir, destination, ct).ConfigureAwait(false);
  }

  /// <summary>
  /// Migrates a version IN PLACE via the bundle-migration API: the produced bundle is uploaded onto the
  /// existing version's artifact prefix, creating no new version.
  ///
  /// <paramref name="authToken"/> is the per-job migration JWT, the only credential available — it authorises
  /// the migration endpoints and nothing else (no GraphQL, no objects api), so the source packfile is
  /// fetched from the presigned url those endpoints hand back. The surrounding lifecycle
  /// (start / complete / fail) belongs to the migration service; a non-zero exit is its failure signal.
  /// </summary>
  private async Task<int> RunInPlace(
    Uri serverUrl,
    string projectId,
    string modelId,
    string versionId,
    string authToken,
    string? outputDirectory,
    CancellationToken ct
  )
  {
    logger.LogInformation("Migrating {ProjectId}/{ModelId}@{VersionId} in place", projectId, modelId, versionId);

    // Attempts are retried, and the upload list is whatever is sitting in the staging directory — so an
    // attempt killed before its files were disposed must not leak into this one. Dispose-time cleanup is
    // best-effort (a kill -9 skips it), so both directories are also emptied up front.
    var packfileDir = EnsureCleanDirectory(Path.Combine(WorkRoot, "packfiles"));
    var stagingDir = EnsureCleanDirectory(outputDirectory ?? Path.Combine(WorkRoot, $"bundle-{versionId}"));

    // The produced filenames aren't known until the bundle exists, and the bundle needs the source — so
    // ask with no files first, purely for the packfile url.
    var source = await migrationClient
      .RequestUploadsAsync(serverUrl, projectId, modelId, versionId, [], authToken, ct)
      .ConfigureAwait(false);

    var packfile = new FileInfo(Path.Combine(packfileDir, $"{versionId}.duckdb"));
    using var packfileHandle = new DisposableFile(packfile, logger);

    // An explicit --out is the caller's to keep; a directory we chose ourselves is ours to clean up.
    var bundleHandles = new List<DisposableFile>();
    try
    {
      using var bare = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };

      logger.LogInformation("Downloading source packfile …");
      await DuckDbHelper
        .DownloadFromUrl(bare, new Uri(source.PackfileDownloadUrl), packfile, new Progress<StreamProgressArgs>(), ct)
        .ConfigureAwait(false);
      logger.LogInformation("Downloaded {Bytes} bytes", packfile.Length);

      using DuckDbTransport duckDbTransport = new DuckDbTransport(new PackFileManager(packfile, activityFactory));
      string rootObjectId = duckDbTransport.GetRootObjectId();

      ITransport readTransport = await EnsureAllObjects(
          duckDbTransport,
          serverUrl,
          projectId,
          rootObjectId,
          authToken,
          ct
        )
        .ConfigureAwait(false);

      var graph = await LoadPackfileGraph(readTransport, rootObjectId).ConfigureAwait(false);
      if (graph is null)
      {
        return 1;
      }

      var files = Produce(graph, stagingDir, versionId);
      foreach (var file in files)
      {
        bundleHandles.Add(
          new DisposableFile(
            new FileInfo(Path.Combine(stagingDir, file)),
            logger,
            deleteOnDispose: outputDirectory is null
          )
        );
      }

      // Sign as late as possible: the presigned urls are short-lived (1h).
      var targets = await migrationClient
        .RequestUploadsAsync(serverUrl, projectId, modelId, versionId, files, authToken, ct)
        .ConfigureAwait(false);

      foreach (var target in targets.Uploads)
      {
        await migrationClient
          .PutFileAsync(bare, target, Path.Combine(stagingDir, target.FileName), ct)
          .ConfigureAwait(false);
      }

      logger.LogInformation(
        "Migration upload OK versionId={VersionId} files={FileCount}",
        versionId,
        targets.Uploads.Count
      );
      return 0;
    }
    finally
    {
      foreach (var handle in bundleHandles)
      {
        handle.Dispose();
      }
    }
  }

  // Everything the harness writes lives under one root it owns, so it can be emptied without touching
  // anything else in %TEMP%.
  private static string WorkRoot => Path.Combine(Path.GetTempPath(), "speckle-artefact-harness");

  // Empties a directory of its files (bundles and packfiles are flat) and returns it. Subdirectories are
  // left alone so that pointing --out at a populated folder can't destroy an unrelated tree.
  private string EnsureCleanDirectory(string dir)
  {
    if (Directory.Exists(dir))
    {
      var stale = Directory.GetFiles(dir);
      foreach (var file in stale)
      {
        File.Delete(file);
      }
      if (stale.Length > 0)
      {
        logger.LogInformation("Removed {StaleCount} pre-existing file(s) from {Directory}", stale.Length, dir);
      }
    }
    Directory.CreateDirectory(dir);
    return dir;
  }

  // Produces the bundle into `outDir` under `baseName`, logging the stats, and returns the produced
  // filenames (basenames).
  private IReadOnlyList<string> Produce(Base root, string outDir, string baseName)
  {
    logger.LogInformation("Deserialized root [{SpeckleType}] id={RootId}", root.speckle_type, root.id);
    logger.LogInformation("Output: {OutDir} (base {BaseName})", outDir, baseName);

    Stats stats;
    using (var producer = producerFactory.Create(outDir, baseName, root))
    {
      stats = producer.Produce(root);
    }

    logger.LogInformation("Produce stats:\n{Stats}", stats);
    foreach (var note in stats.Notes)
    {
      logger.LogInformation("Note: {Note}", note);
    }

    var files = new List<string>();
    foreach (var f in Directory.GetFiles(outDir).OrderBy(x => x, StringComparer.Ordinal))
    {
      logger.LogInformation("Bundle file {FileName} {Bytes} bytes", Path.GetFileName(f), new FileInfo(f).Length);
      files.Add(Path.GetFileName(f));
    }
    return files;
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
  private async Task<Base> LoadFromServerPackfile(
    Uri serverUrl,
    string projectId,
    string modelId,
    string? versionId,
    string authToken,
    CancellationToken ct
  )
  {
    if (versionId is null)
    {
      logger.LogInformation("Resolving latest version of {ProjectId}/{ModelId} …", projectId, modelId);
      (versionId, _) = await remoteSource
        .ResolveLatestVersionAsync(serverUrl, projectId, modelId, authToken, ct)
        .ConfigureAwait(false);
      logger.LogInformation("Latest version {VersionId}", versionId);
    }

    var packfileDir = EnsureCleanDirectory(Path.Combine(WorkRoot, "packfiles"));
    var packfile = new FileInfo(Path.Combine(packfileDir, $"{versionId}.duckdb"));
    using var packfileHandle = new DisposableFile(packfile, logger);

    using var http = new HttpClient { BaseAddress = serverUrl };
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
    // Cloudflare (app.speckle.systems) 403s requests with no User-Agent.
    http.DefaultRequestHeaders.UserAgent.ParseAdd("speckle-artefact-harness/1.0");

    logger.LogInformation("Downloading packfile for version {VersionId} …", versionId);
    await DuckDbHelper
      .DownloadDuckFile(http, projectId, modelId, versionId, packfile, new Progress<StreamProgressArgs>(), ct)
      .ConfigureAwait(false);
    logger.LogInformation("Downloaded {Bytes} bytes", packfile.Length);

    using DuckDbTransport duckDbTransport = new DuckDbTransport(new PackFileManager(packfile, activityFactory));
    string rootObjectId = duckDbTransport.GetRootObjectId();

    ITransport readTransport = await EnsureAllObjects(
        duckDbTransport,
        serverUrl,
        projectId,
        rootObjectId,
        authToken,
        ct
      )
      .ConfigureAwait(false);

    return await LoadPackfileGraph(readTransport, rootObjectId).ConfigureAwait(false);
  }

  /// <summary>
  /// Most packfiles hold the whole graph, but a few on the server are missing objects. Checks the packfile
  /// against its own root closure and, only if something is absent, pulls the missing objects from the
  /// server's REST api into a <see cref="MemoryTransport"/> — nothing is written back to the packfile.
  /// Returns the transport to read the graph from.
  /// </summary>
  private async Task<ITransport> EnsureAllObjects(
    DuckDbTransport duckDbTransport,
    Uri serverUrl,
    string projectId,
    string rootObjectId,
    string authToken,
    CancellationToken ct
  )
  {
    var rootJson = await duckDbTransport
      .GetObject(rootObjectId)
      .NotNull($"Packfile does not contain its own root object '{rootObjectId}'")
      .ConfigureAwait(false);

    var childrenIds = ClosureIds(rootJson, ct);
    var found = await duckDbTransport.HasObjects(childrenIds).ConfigureAwait(false);
    var missingCount = found.Count(kvp => !kvp.Value);
    if (missingCount == 0)
    {
      logger.LogInformation("Packfile is complete ({ObjectCount} objects in root closure)", childrenIds.Count);
      return duckDbTransport;
    }

    logger.LogWarning(
      "Packfile is missing {MissingCount} of {ObjectCount} objects — filling from {ServerUrl}",
      missingCount,
      childrenIds.Count,
      serverUrl
    );

    var memoryTransport = new MemoryTransport { CancellationToken = ct };
    // Reads hit the packfile first, then whatever we fill; writes land in memory only (the packfile is
    // read-only). The server must NOT be a read transport here — see AggregateTransport's remarks.
    AggregateTransport local = new([duckDbTransport, memoryTransport], [memoryTransport]);

    Account account = new()
    {
      token = authToken,
      userInfo = new(),
      serverInfo = new() { url = serverUrl.ToString().TrimEnd('/') },
    };
    using var remote = serverTransportFactory.Create(account, projectId);
    remote.CancellationToken = ct;

    // Downloads the root, diffs the server's root closure against `local`, and saves only what's absent.
    await remote.CopyObjectAndChildren(rootObjectId, local).ConfigureAwait(false);
    logger.LogInformation("Filled {FilledCount} object(s) from {ServerUrl}", memoryTransport.Objects.Count, serverUrl);

    var stillMissing = (await local.HasObjects(childrenIds).ConfigureAwait(false))
      .Where(kvp => !kvp.Value)
      .Select(kvp => kvp.Key)
      .ToList();

    // Anything still absent is absent from both sides, so the graph can't be completed — fail with the ids
    // rather than let the deserialize die on a bare KeyNotFoundException.
    if (stillMissing.Count > 0)
    {
      throw new TransportException(
        $"{stillMissing.Count} object(s) are missing from both the packfile and {serverUrl}, "
          + $"e.g. {string.Join(", ", stillMissing.Take(5))}"
      );
    }

    return local;
  }

  // Every descendant id from an object's closure. Blob refs aren't rows in `objects`, so they're excluded using
  // the same predicate ServerTransport applies to its own diff.
  private static List<string> ClosureIds(string json, CancellationToken ct) =>
    ClosureParser.GetChildrenIds(json, ct).Where(x => !x.Contains("blob:")).ToList();

  // Reads a DuckDB packfile into a Base graph. The transport stays alive for the whole deserialize (children
  // are fetched from it by id).
  private async Task<Base> LoadPackfileGraph(ITransport transport, string rootId)
  {
    var rootJson = await transport
      .GetObject(rootId)
      .NotNull($"Root '{rootId}' not found in packfile")
      .ConfigureAwait(false);

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

    // Emptied up front: the uploader renames and ships EVERY file in this directory, so anything left over
    // from a previous run would be uploaded as part of this bundle.
    outDir = EnsureCleanDirectory(
      outDir ?? Path.Combine(Path.GetTempPath(), $"speckle-artefact-{baseName}-{DateTime.UtcNow:yyyyMMddHHmmss}")
    );
    logger.LogInformation("Output: {OutDir} (base {BaseName})", outDir, baseName);

    Stats stats;
    using (var producer = producerFactory.Create(outDir, baseName, root))
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
    // SPIKE(ticket-17, local-only): anonymous when token empty; UA required through Cloudflare.
    if (!string.IsNullOrEmpty(token))
    {
      http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }
    http.DefaultRequestHeaders.UserAgent.ParseAdd("speckle-backfill-validation/1.0");
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
}
