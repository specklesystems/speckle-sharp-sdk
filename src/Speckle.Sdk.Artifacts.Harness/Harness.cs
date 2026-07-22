using System.IO.Compression;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Artifacts.Harness;

/// <summary>
/// End-to-end artefact-bundle harness: resolves an object graph (local NDJSON OR remote
/// server), produces the parquet bundle on disk, and optionally uploads it via the v2
/// envelope-bundle flow. Registered in the DI container and resolved from <c>Program</c>.
/// </summary>
internal sealed class Harness(
  RemoteSource remoteSource,
  GraphArtifactProducer2Factory producerFactory,
  BundleUploader bundleUploader,
  SgeoSelfTest selfTest,
  ILogger<Harness> logger
)
{
  public async Task<int> Execute(string[] argv)
  {
    if (argv.Length == 1 && argv[0] == "--selftest")
    {
      return selfTest.Run();
    }

    Options opts;
    try
    {
      opts = Options.Parse(argv);
    }
    catch (ArgumentException ex)
    {
      logger.LogError("Invalid arguments: {Error}\n{Usage}", ex.Message, Options.Usage);
      return 2;
    }

    // ── resolve the graph (local file OR remote server) ─────────────────────────────────
    Base root;
    string baseName;
    if (opts.Mode == InputMode.Local)
    {
      var (localRoot, localBaseName) = await LoadLocal(opts).ConfigureAwait(false);
      if (localRoot is null)
      {
        return 1;
      }
      root = localRoot;
      baseName = localBaseName;
    }
    else
    {
      var token = RequireEnv("SPECKLE_SRC_TOKEN");
      if (token is null)
      {
        return 3;
      }

      var serverUrl = new Uri(opts.SrcServerUrl.NotNull());
      var projectId = opts.SrcProjectId.NotNull();
      var modelId = opts.SrcModelId.NotNull();

      string rootId;
      if (opts.SrcVersionId is not null)
      {
        // Caller pinned a version; we still need its rootId. Resolve it via GraphQL by
        // listing the version (cheapest reliable path without an extra single-version query).
        logger.LogInformation("Resolving rootId for version {VersionId} …", opts.SrcVersionId);
        var (vId, rId) = await ResolveVersionRootId(serverUrl, projectId, modelId, opts.SrcVersionId, token)
          .ConfigureAwait(false);
        rootId = rId;
        logger.LogInformation("Version {VersionId} → rootId {RootId}", vId, rootId);
      }
      else
      {
        logger.LogInformation("Resolving latest version of {ProjectId}/{ModelId} …", projectId, modelId);
        var (vId, rId) = await remoteSource
          .ResolveLatestVersionAsync(serverUrl, projectId, modelId, token, CancellationToken.None)
          .ConfigureAwait(false);
        rootId = rId;
        logger.LogInformation("Latest version {VersionId} → rootId {RootId}", vId, rootId);
      }

      logger.LogInformation("Deserializing from server …");
      root = await remoteSource
        .DeserializeFromServerAsync(serverUrl, projectId, rootId, token, CancellationToken.None)
        .ConfigureAwait(false);
      baseName = modelId;
    }

    logger.LogInformation("Deserialized root [{SpeckleType}] id={RootId}", root.speckle_type, root.id);

    // ── produce the bundle on disk ───────────────────────────────────────────────────────
    var outDir =
      opts.OutDir ?? Path.Combine(Path.GetTempPath(), $"speckle-artefact-{baseName}-{DateTime.UtcNow:yyyyMMddHHmmss}");
    logger.LogInformation("Output: {OutDir} (base {BaseName})", outDir, baseName);

    GraphArtifactProducer2.Stats stats;
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

    // ── optionally upload via the v2 envelope-bundle flow ────────────────────────────────
    if (opts.Mode == InputMode.Local && opts.Upload && root.id is null)
    {
      logger.LogError(
        "Cannot upload — root.id is null (a locally-built graph that was not deserialised from a hashed source has no id)."
      );
      return 4;
    }

    if (opts.Upload)
    {
      string? dstToken = RequireEnv("SPECKLE_DST_TOKEN");
      if (dstToken is null)
      {
        return 3;
      }

      string rootId = root.id.NotNull();
      // totalChildrenCount: best-effort = (object count - 1) for the root. Server stores it on
      // the commit; not load-bearing for serving. See README "uncertainties".
      int? totalChildrenCount = stats.Objects > 0 ? Math.Max(0, stats.Objects - 1) : null;
      Uri url = new(opts.DstServerUrl.NotNull());
      logger.LogInformation("Uploading (v2 envelope bundle) …");
      var result = await bundleUploader
        .UploadAsync(
          url,
          opts.DstProjectId.NotNull(),
          opts.DstModelId.NotNull(),
          outDir,
          rootId,
          totalChildrenCount,
          dstToken,
          CancellationToken.None
        )
        .ConfigureAwait(false);

      Uri viewerUrl = new(url, $"/projects/{opts.DstProjectId}/models/{opts.DstModelId}@{result.VersionId}");
      logger.LogInformation("Upload OK versionId={VersionId} files={FileCount}", result.VersionId, result.Files.Count);
      logger.LogInformation("Viewer: {ViewerUrl}", viewerUrl);
    }

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
  private async Task<(Base? root, string baseName)> LoadLocal(Options opts)
  {
    var ndjsonPath = new FileInfo(opts.LocalPath.NotNull());
    var baseName = ndjsonPath.Name;
    logger.LogInformation("Input: {InputPath}", ndjsonPath);

    var transport = new MemoryTransport();
    var jsonById = new Dictionary<string, string>(StringComparer.Ordinal);
    var lineCount = 0;
    foreach (var line in ReadLines(ndjsonPath))
    {
      if (line.Length == 0)
      {
        continue;
      }
      var parts = line.Split('\t');
      if (parts.Length < 2)
      {
        continue;
      }
      var id = parts[0];
      var json = parts[^1]; // last field is always the payload json
      transport.SaveObject(id, json);
      jsonById[id] = json;
      lineCount++;
    }
    logger.LogInformation("Loaded {LineCount} objects into transport", lineCount);

    var rootId = opts.LocalRoot == "auto" ? DetectRoot(jsonById) : opts.LocalRoot;
    if (rootId is null || !jsonById.TryGetValue(rootId, out var rootJson))
    {
      logger.LogError("Root '{RootId}' not found. Available collection-like candidates:", rootId);
      foreach (var c in CollectionCandidates(jsonById).Take(10))
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

  private string? RequireEnv(string name)
  {
    var val = Environment.GetEnvironmentVariable(name);
    if (string.IsNullOrWhiteSpace(val))
    {
      logger.LogError("Required environment variable {EnvVar} is not set", name);
      return null;
    }
    return val;
  }

  // ── helpers (local mode) ────────────────────────────────────────────────────────────────

  private static IEnumerable<string> ReadLines(FileInfo path)
  {
    ZipArchive? archive = null;
    try
    {
      Stream raw = path.OpenRead();
      Stream stream;
      switch (path.Extension)
      {
        case ".gz":
          stream = new GZipStream(raw, CompressionMode.Decompress);
          break;
        case ".zip":
          archive = new ZipArchive(raw, ZipArchiveMode.Read);
          stream = archive.Entries[0].Open();
          break;
        default:
          stream = raw;
          break;
      }

      using StreamReader reader = new(stream);
      while (reader.ReadLine() is { } line)
      {
        yield return line;
      }
    }
    finally
    {
      archive?.Dispose();
    }
  }

  private static string? DetectRoot(Dictionary<string, string> jsonById)
  {
    var referenced = new HashSet<string>(StringComparer.Ordinal);
    foreach (var json in jsonById.Values)
    {
      foreach (Match m in Regex.Matches(json, "\"referencedId\":\"([0-9a-fA-F]+)\""))
      {
        referenced.Add(m.Groups[1].Value);
      }
    }
    var unreferenced = jsonById.Keys.Where(id => !referenced.Contains(id)).ToList();
    var pool = unreferenced.Count > 0 ? unreferenced : jsonById.Keys.ToList();
    return pool.OrderByDescending(id => LooksLikeCollection(jsonById[id]) ? 1 : 0)
      .ThenByDescending(id => jsonById[id].Length)
      .FirstOrDefault();
  }

  private static IEnumerable<string> CollectionCandidates(Dictionary<string, string> jsonById) =>
    jsonById.Where(kv => LooksLikeCollection(kv.Value)).OrderByDescending(kv => kv.Value.Length).Select(kv => kv.Key);

  private static bool LooksLikeCollection(string json) =>
    json.Contains("Collection", StringComparison.Ordinal)
    || json.Contains("instanceDefinitionProxies", StringComparison.Ordinal)
    || json.Contains("renderMaterialProxies", StringComparison.Ordinal);
}
