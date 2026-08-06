// SPIKE(wayfinder ticket 07) — THROWAWAY.
// Proves/kills the crafted-referencedObject dispatch end-to-end on .NET:
//   1. PRODUCE  a Revit-ish artefact bundle locally (ObjectsArtifactPipeline) — stands in for the server's
//               stored bundle; the real server side of the crafted id does not exist yet (ticket 01).
//   2. SERVE    the /v2 artifacts contract from an in-process HTTP stub: the list endpoint (auth required,
//               presigned urls in the response) + the "S3" blob urls (no auth) — same shapes as production.
//   3. RECEIVE  via Operations.Receive2(url, projectId, "projectId;modelId;versionId", token) — the real
//               SDK path: dispatch → ArtifactDownloader → ArtefactBundleReader → ObjectsArtifactReader.
//   4. TRAVERSE the result the way a v3-era script would (nested parameters, displayValue meshes) and
//               print exactly where fidelity falls short of the legacy graph.
//   5. FAIL     the legacy transport Receive on the crafted id (loud, pointed error).
using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Objects.Data;
using Speckle.Objects.Geometry;
using Speckle.Objects.Utils;
using Speckle.Sdk;
using Speckle.Sdk.Api;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;
using Speckle.Sdk.Pipelines.Receive.Artifacts;
using Speckle.Sdk.Transports;

const string ProjectId = "spikeproj01";
const string ModelId = "spikemodel1";
const string VersionId = "spikever001";
const string Token = "spike-token-abc";
string craftedId = $"{ProjectId};{ModelId};{VersionId}";

var bundleSourceDir = Path.Combine(Path.GetTempPath(), "SpeckleSpike07-producer", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(bundleSourceDir);

// ── 1. PRODUCE ────────────────────────────────────────────────────────────────────────────────────────────
var producer = new SpeckleApplication
{
  HostApplication = "Spike07",
  HostApplicationVersion = "0.0.1",
  Slug = "spike07",
  SpeckleVersion = "999.0.0-spike",
};

using (var pipeline = new ObjectsArtifactPipeline(bundleSourceDir, VersionId, producer))
{
  int levelK = pipeline.AddLevel("level-1", "Level 1", 0.0);
  int layerK = pipeline.AddCollection("walls-cat", "Walls", null, "Category");

  // A Revit-ish wall: nested instance parameters + type parameters (type-scoped rows go to the type tables).
  int wallK = pipeline.InternObject("wall-1");
  pipeline.AddProperties(
    "wall-1",
    new Dictionary<string, object?>
    {
      ["Parameters"] = new Dictionary<string, object?>
      {
        ["Dimensions"] = new Dictionary<string, object?> { ["Area"] = 24.5, ["Volume"] = 7.35 },
        ["Type Parameters"] = new Dictionary<string, object?> { ["Width"] = 0.3, ["Fire Rating"] = "2hr" },
      },
    },
    new[]
    {
      new KeyValuePair<string, object?>("name", "Basic Wall"),
      new KeyValuePair<string, object?>("units", "m"),
      new KeyValuePair<string, object?>("speckle_type", "Objects.BuiltElements.Wall"),
    },
    typeKey: "walltype-200mm"
  );
  var wallMesh = new Mesh
  {
    vertices = new List<double> { 0, 0, 0, 5, 0, 0, 5, 0, 3, 0, 0, 3 },
    faces = new List<int> { 4, 0, 1, 2, 3 },
    units = "m",
  };
  int gK = pipeline.AddGeometry("wall-1:g0", wallMesh);
  pipeline.Display(wallK, gK, 0);
  pipeline.InCollection(wallK, layerK, 0);
  pipeline.OnLevel(wallK, levelK);

  // A property-only element (a Room): no geometry, no DISPLAY edge — the reader's known skip case.
  int roomK = pipeline.InternObject("room-1");
  pipeline.AddProperties(
    "room-1",
    new Dictionary<string, object?>
    {
      ["Parameters"] = new Dictionary<string, object?> { ["Number"] = "101", ["Occupancy"] = "Office" },
    },
    new[]
    {
      new KeyValuePair<string, object?>("name", "Room 101"),
      new KeyValuePair<string, object?>("units", "m"),
    }
  );
  pipeline.InCollection(roomK, layerK, 1);

  pipeline.Complete();
}

var producedFiles = Directory.GetFiles(bundleSourceDir).Select(Path.GetFileName).OrderBy(n => n).ToList();
Console.WriteLine($"[produce] bundle written: {producedFiles.Count} files");
foreach (var f in producedFiles)
{
  Console.WriteLine($"[produce]   {f}");
}

// ── 2. SERVE ──────────────────────────────────────────────────────────────────────────────────────────────
int port = 18077;
var listener = new HttpListener();
listener.Prefixes.Add($"http://localhost:{port}/");
listener.Start();
var serverLog = new List<string>();
var serverCts = new CancellationTokenSource();
var serverTask = Task.Run(async () =>
{
  while (!serverCts.IsCancellationRequested)
  {
    HttpListenerContext ctx;
    try
    {
      ctx = await listener.GetContextAsync();
    }
    catch (Exception) when (serverCts.IsCancellationRequested)
    {
      break;
    }
    string path = ctx.Request.Url!.AbsolutePath;
    string? auth = ctx.Request.Headers["Authorization"];
    if (path == $"/api/v2/projects/{ProjectId}/models/{ModelId}/versions/{VersionId}/artifacts")
    {
      serverLog.Add($"LIST   {path} auth={(auth is null ? "NONE" : auth.StartsWith("Bearer ") ? "Bearer …" : auth)}");
      if (auth != $"Bearer {Token}")
      {
        ctx.Response.StatusCode = 401;
        ctx.Response.Close();
        continue;
      }
      var files = Directory
        .GetFiles(bundleSourceDir)
        .Select(Path.GetFileName)
        .Select(n => $"{{\"name\":\"{n}\",\"url\":\"http://localhost:{port}/fake-s3/{n}?sig=presigned-opaque\"}}");
      byte[] body = Encoding.UTF8.GetBytes($"{{\"files\":[{string.Join(",", files)}]}}");
      ctx.Response.ContentType = "application/json";
      await ctx.Response.OutputStream.WriteAsync(body);
      ctx.Response.Close();
    }
    else if (path.StartsWith("/fake-s3/"))
    {
      serverLog.Add($"BLOB   {Path.GetFileName(path)} auth={(auth is null ? "none (presigned)" : "UNEXPECTED " + auth)}");
      string file = Path.Combine(bundleSourceDir, Path.GetFileName(path));
      if (!File.Exists(file))
      {
        ctx.Response.StatusCode = 404;
        ctx.Response.Close();
        continue;
      }
      byte[] bytes = await File.ReadAllBytesAsync(file);
      await ctx.Response.OutputStream.WriteAsync(bytes);
      ctx.Response.Close();
    }
    else
    {
      serverLog.Add($"404    {path}");
      ctx.Response.StatusCode = 404;
      ctx.Response.Close();
    }
  }
});
Console.WriteLine($"[serve] /v2 artifacts stub on http://localhost:{port}");

// ── 3. RECEIVE ────────────────────────────────────────────────────────────────────────────────────────────
var services = new ServiceCollection();
services.AddSpeckleSdk(new Speckle.Sdk.Application("Spike07", "spike07"), "0.0.1", typeof(DataObject).Assembly);
services.AddSingleton<IArtifactGraphMaterializer, SpikeArtifactGraphMaterializer>();
var provider = services.BuildServiceProvider();
var operations = provider.GetRequiredService<IOperations>();

Console.WriteLine($"[receive] Operations.Receive2(referencedObject: \"{craftedId}\")");
var root = await operations.Receive2(
  new Uri($"http://localhost:{port}"),
  ProjectId,
  craftedId,
  Token,
  onProgressAction: null,
  cancellationToken: CancellationToken.None
);

foreach (var line in serverLog)
{
  Console.WriteLine($"[serve]   {line}");
}

// ── 4. TRAVERSE like a v3 script ─────────────────────────────────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("=== received tree ===");
PrintTree(root, 0);

static void PrintTree(Base b, int depth)
{
  string pad = new(' ', depth * 2);
  string label = b switch
  {
    Collection c => $"Collection(name={c.name})",
    DataObject d => $"DataObject(name={d.name}, displayValue={d.displayValue.Count})",
    _ => b.speckle_type,
  };
  Console.WriteLine($"{pad}{label}  [speckle_type={b.speckle_type}, id={b.id ?? "<null>"}, applicationId={b.applicationId ?? "<null>"}]");
  if (b is Collection col)
  {
    foreach (var child in col.elements)
    {
      PrintTree(child, depth + 1);
    }
  }
}

Console.WriteLine();
Console.WriteLine("=== v3-script-style access ===");
var wall = FindByAppId(root, "wall-1") as DataObject ?? throw new InvalidOperationException("wall-1 not found");

// the properties dict exactly as the reader hands it back:
Console.WriteLine("wall.properties =");
DumpDict(wall.properties, 1);

static void DumpDict(IReadOnlyDictionary<string, object?> d, int depth)
{
  string pad = new(' ', depth * 2);
  foreach (var kv in d)
  {
    if (kv.Value is Dictionary<string, object?> nested)
    {
      Console.WriteLine($"{pad}{kv.Key}:");
      DumpDict(nested, depth + 1);
    }
    else
    {
      Console.WriteLine($"{pad}{kv.Key} = {kv.Value ?? "<null>"} ({kv.Value?.GetType().Name ?? "-"})");
    }
  }
}

// nested parameter access, the `obj.parameters["Area"]` analog (path shape verified by the dump above):
object? Walk(IReadOnlyDictionary<string, object?> d, params string[] path)
{
  object? cur = d;
  foreach (var seg in path)
  {
    if (cur is not Dictionary<string, object?> dd && (cur = (cur as IReadOnlyDictionary<string, object?>)) is null)
    {
      return $"<no such path: {string.Join(".", path)}>";
    }
    dd = (Dictionary<string, object?>)cur!;
    if (!dd.TryGetValue(seg, out cur))
    {
      return $"<no such path: {string.Join(".", path)} (stuck at {seg})>";
    }
  }
  return cur;
}

Console.WriteLine($"Parameters.Dimensions.Area   = {Walk(wall.properties, "Parameters", "Dimensions", "Area")}");
Console.WriteLine($"Parameters.Dimensions.Volume = {Walk(wall.properties, "Parameters", "Dimensions", "Volume")}");
Console.WriteLine($"properties.Parameters.Dimensions.Area (prefixed shape) = {Walk(wall.properties, "properties", "Parameters", "Dimensions", "Area")}");
Console.WriteLine($"Type Parameters came back somewhere? = {FindKey(wall.properties, "Type Parameters") ?? "NO (were sent: Width=0.3, Fire Rating=2hr)"}");

static string? FindKey(IReadOnlyDictionary<string, object?> d, string key, string prefix = "")
{
  foreach (var kv in d)
  {
    string here = prefix.Length == 0 ? kv.Key : $"{prefix}.{kv.Key}";
    if (kv.Key == key)
    {
      return here;
    }
    if (kv.Value is Dictionary<string, object?> nested && FindKey(nested, key, here) is { } hit)
    {
      return hit;
    }
  }
  return null;
}

var mesh0 = wall.displayValue.OfType<Mesh>().First();
Console.WriteLine($"wall.displayValue[0]: Mesh vertices={mesh0.vertices.Count / 3} faces(list len)={mesh0.faces.Count} units={mesh0.units}");
Console.WriteLine($"wall speckle_type sent='Objects.BuiltElements.Wall' received='{wall.speckle_type}'");
Console.WriteLine($"wall id (content hash in legacy)                          = '{wall.id ?? "<null>"}' vs applicationId='{wall.applicationId}'");
Console.WriteLine($"room-1 (property-only element) present in tree?           = {FindByAppId(root, "room-1") is not null}");

static Base? FindByAppId(Base b, string appId)
{
  if (b.applicationId == appId)
  {
    return b;
  }
  if (b is Collection c)
  {
    foreach (var child in c.elements)
    {
      if (FindByAppId(child, appId) is { } hit)
      {
        return hit;
      }
    }
  }
  return null;
}

// ── 5. LEGACY TRANSPORT PATH must fail loud ──────────────────────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("=== legacy Receive(objectId, ITransport) with crafted id ===");
try
{
  await operations.Receive(craftedId, remoteTransport: null, localTransport: new MemoryTransport());
  Console.WriteLine("UNEXPECTED: legacy Receive accepted the crafted id");
}
catch (NotSupportedException ex)
{
  Console.WriteLine($"OK, failed loud: {ex.Message}");
}

serverCts.Cancel();
listener.Stop();
Directory.Delete(bundleSourceDir, true);
Console.WriteLine();
Console.WriteLine("spike complete");
