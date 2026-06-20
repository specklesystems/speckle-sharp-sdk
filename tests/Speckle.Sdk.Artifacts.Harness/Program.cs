using System.IO.Compression;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Objects.Geometry;
using Speckle.Sdk;
using Speckle.Sdk.Artifacts.Harness;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Transports;

// ── CLI ───────────────────────────────────────────────────────────────────────────────
// INPUT (pick one):
//   --local <ndjsonPath> [--root <id|auto>]
//   --remote <serverUrl> <projectId> <modelId> [--version <versionId>]   (token: SPECKLE_SRC_TOKEN)
// OUTPUT:
//   --out <dir>                                  (default: temp dir)
//   --upload <serverUrl> <projectId> <modelId>   (token: SPECKLE_DST_TOKEN)
// Both --out and --upload may apply. --upload implies a temp dir if --out is absent.
//
// Backwards-compat: if the first arg is not a recognised flag, falls back to the legacy
// positional form `<ndjsonPath> [rootId|auto] [outDir]`.
return await Run(args).ConfigureAwait(false);

async Task<int> Run(string[] argv)
{
  Options opts;
  try
  {
    opts = Options.Parse(argv);
  }
  catch (ArgumentException ex)
  {
    Console.Error.WriteLine($"error: {ex.Message}");
    Console.Error.WriteLine(Options.Usage);
    return 2;
  }

  // ── init the Speckle type registry (so the deserializer yields TYPED proxies/meshes) ──
  var sc = new ServiceCollection();
  sc.AddSpeckleSdk(new("ArtefactHarness", "artefact-harness"), "v3", typeof(Mesh).Assembly);
  var serviceProvider = sc.BuildServiceProvider();

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

    var serverUrl = opts.SrcServerUrl!;
    var projectId = opts.SrcProjectId!;
    var modelId = opts.SrcModelId!;

    string rootId;
    if (opts.SrcVersionId is not null)
    {
      // Caller pinned a version; we still need its rootId. Resolve it via GraphQL by
      // listing the version (cheapest reliable path without an extra single-version query).
      Console.WriteLine($"Resolving rootId for version {opts.SrcVersionId} …");
      var (vId, rId) = await ResolveVersionRootId(
        serverUrl,
        projectId,
        modelId,
        opts.SrcVersionId,
        token
      ).ConfigureAwait(false);
      rootId = rId;
      Console.WriteLine($"Version {vId} → rootId {rootId}");
    }
    else
    {
      Console.WriteLine($"Resolving latest version of {projectId}/{modelId} …");
      var (vId, rId) = await RemoteSource
        .ResolveLatestVersionAsync(serverUrl, projectId, modelId, token, CancellationToken.None)
        .ConfigureAwait(false);
      rootId = rId;
      Console.WriteLine($"Latest version {vId} → rootId {rootId}");
    }

    Console.WriteLine("Deserializing from server …");
    root = await RemoteSource
      .DeserializeFromServerAsync(
        serviceProvider,
        serverUrl,
        projectId,
        rootId,
        token,
        CancellationToken.None
      )
      .ConfigureAwait(false);
    baseName = modelId;
  }

  Console.WriteLine($"Deserialized root [{root.speckle_type}] id={root.id}");

  // ── produce the bundle on disk ───────────────────────────────────────────────────────
  var outDir = opts.OutDir ?? Path.Combine(Path.GetTempPath(), $"speckle-artefact-{baseName}-{DateTime.UtcNow:yyyyMMddHHmmss}");
  Console.WriteLine($"Output: {outDir}  (base '{baseName}')");

  var stats = GraphArtifactProducer.Produce(root, outDir, baseName);

  Console.WriteLine();
  Console.WriteLine("──────── PRODUCE STATS ────────");
  Console.WriteLine(stats);
  if (stats.Notes.Count > 0)
  {
    Console.WriteLine("notes:");
    foreach (var n in stats.Notes)
    {
      Console.WriteLine($"  • {n}");
    }
  }
  Console.WriteLine();
  Console.WriteLine("──────── BUNDLE FILES ────────");
  foreach (var f in Directory.GetFiles(outDir).OrderBy(x => x))
  {
    Console.WriteLine($"  {Path.GetFileName(f),-40} {new FileInfo(f).Length,12:N0} bytes");
  }

  // ── optionally upload via the v2 envelope-bundle flow ────────────────────────────────
  if (opts.Mode == InputMode.Local && opts.Upload && root.id is null)
  {
    Console.Error.WriteLine(
      "error: cannot upload — root.id is null (a locally-built graph that was not deserialised from a hashed source has no id)."
    );
    return 4;
  }

  if (opts.Upload)
  {
    var dstToken = RequireEnv("SPECKLE_DST_TOKEN");
    if (dstToken is null)
    {
      return 3;
    }

    var rootId = root.id!;
    // totalChildrenCount: best-effort = (object count - 1) for the root. Server stores it on
    // the commit; not load-bearing for serving. See README "uncertainties".
    int? totalChildrenCount = stats.Objects > 0 ? Math.Max(0, stats.Objects - 1) : null;

    Console.WriteLine();
    Console.WriteLine("──────── UPLOAD (v2 envelope bundle) ────────");
    var result = await BundleUploader
      .UploadAsync(
        opts.DstServerUrl!,
        opts.DstProjectId!,
        opts.DstModelId!,
        outDir,
        rootId,
        totalChildrenCount,
        dstToken,
        CancellationToken.None
      )
      .ConfigureAwait(false);

    var viewerUrl =
      $"{opts.DstServerUrl!.TrimEnd('/')}/projects/{opts.DstProjectId}/models/{opts.DstModelId}@{result.VersionId}";
    Console.WriteLine();
    Console.WriteLine($"UPLOAD OK  versionId={result.VersionId}  files={result.Files.Count}");
    Console.WriteLine($"Viewer: {viewerUrl}");
  }

  return 0;
}

// Resolve a specific version's referencedObject (rootId) via GraphQL.
async Task<(string versionId, string rootId)> ResolveVersionRootId(
  string serverUrl,
  string projectId,
  string modelId,
  string versionId,
  string token
)
{
  // Reuse the latest-version resolver if no pin; otherwise query the single version.
  using var http = new System.Net.Http.HttpClient();
  http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
    "Bearer",
    token
  );
  const string query =
    @"query Version($projectId: String!, $modelId: String!, $versionId: String!) {
  project(id: $projectId) {
    model(id: $modelId) {
      version(id: $versionId) { id referencedObject }
    }
  }
}";
  var payload = System.Text.Json.JsonSerializer.Serialize(
    new { query, variables = new { projectId, modelId, versionId } }
  );
  using var content = new System.Net.Http.StringContent(
    payload,
    System.Text.Encoding.UTF8,
    "application/json"
  );
  using var resp = await http
    .PostAsync(serverUrl.TrimEnd('/') + "/graphql", content)
    .ConfigureAwait(false);
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
  var v = doc
    .RootElement.GetProperty("data")
    .GetProperty("project")
    .GetProperty("model")
    .GetProperty("version");
  return (v.GetProperty("id").GetString()!, v.GetProperty("referencedObject").GetString()!);
}

// ── local ndjson → Base graph (existing behaviour) ──────────────────────────────────────
async Task<(Base? root, string baseName)> LoadLocal(Options opts)
{
  var ndjsonPath = opts.LocalPath!;
  var baseName = Path.GetFileName(ndjsonPath).Split('.')[0];
  Console.WriteLine($"Input : {ndjsonPath}");

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
  Console.WriteLine($"Loaded {lineCount} objects into transport.");

  var rootId = opts.LocalRoot == "auto" ? DetectRoot(jsonById) : opts.LocalRoot;
  if (rootId is null || !jsonById.TryGetValue(rootId, out var rootJson))
  {
    Console.Error.WriteLine($"Root '{rootId}' not found. Available collection-like candidates:");
    foreach (var c in CollectionCandidates(jsonById).Take(10))
    {
      Console.Error.WriteLine($"  {c}");
    }
    return (null, baseName);
  }
  Console.WriteLine($"Root  : {rootId}");

  var deserializer = new SpeckleObjectDeserializer { ReadTransport = transport };
  var root = await deserializer.DeserializeAsync(rootJson).ConfigureAwait(false);
  return (root, baseName);
}

static string? RequireEnv(string name)
{
  var val = Environment.GetEnvironmentVariable(name);
  if (string.IsNullOrWhiteSpace(val))
  {
    Console.Error.WriteLine($"error: required environment variable {name} is not set.");
    return null;
  }
  return val;
}

// ── helpers (local mode) ────────────────────────────────────────────────────────────────

static IEnumerable<string> ReadLines(string path)
{
  Stream raw = File.OpenRead(path);
  Stream stream = path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
    ? new GZipStream(raw, CompressionMode.Decompress)
    : path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
      ? new ZipArchive(raw, ZipArchiveMode.Read).Entries[0].Open()
      : raw;
  using var reader = new StreamReader(stream);
  string? line;
  while ((line = reader.ReadLine()) is not null)
  {
    yield return line;
  }
}

static string? DetectRoot(Dictionary<string, string> jsonById)
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
  return pool
    .OrderByDescending(id => LooksLikeCollection(jsonById[id]) ? 1 : 0)
    .ThenByDescending(id => jsonById[id].Length)
    .FirstOrDefault();
}

static IEnumerable<string> CollectionCandidates(Dictionary<string, string> jsonById) =>
  jsonById
    .Where(kv => LooksLikeCollection(kv.Value))
    .OrderByDescending(kv => kv.Value.Length)
    .Select(kv => kv.Key);

static bool LooksLikeCollection(string json) =>
  json.Contains("Collection", StringComparison.Ordinal)
  || json.Contains("instanceDefinitionProxies", StringComparison.Ordinal)
  || json.Contains("renderMaterialProxies", StringComparison.Ordinal);
