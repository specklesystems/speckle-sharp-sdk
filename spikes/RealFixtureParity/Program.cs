// SPIKE(wayfinder ticket 17) — THROWAWAY.
// Real-source fixture pair: receives the SAME real Revit version down both rails and dumps a
// comparable walk of each tree as JSON:
//   legacy <serverUrl> <projectId> <rootId> <outJson>
//       — the SDK's legacy server receive (real /objects API, no stub) via Operations.Receive2.
//   bundle <bundleDir> <projectId> <modelId> <versionId> <outJson>
//       — serves <bundleDir> through the ticket-07 in-process /v2 artifacts stub, then receives via
//         the crafted-referencedObject dispatch (Receive2 → ArtifactDownloader → ObjectsArtifactReader).
// The two JSON dumps are diffed outside (python) into the parity/loss report.
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Objects.Data;
using Speckle.Objects.Geometry;
using Speckle.Objects.Utils;
using Speckle.Sdk;
using Speckle.Sdk.Api;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;
using Speckle.Sdk.Pipelines.Receive.Artifacts;

if (args.Length < 1)
{
  Console.Error.WriteLine("usage: legacy <serverUrl> <projectId> <rootId> <outJson> | bundle <bundleDir> <projectId> <modelId> <versionId> <outJson>");
  return 2;
}

var services = new ServiceCollection();
services.AddSpeckleSdk(new Speckle.Sdk.Application("Parity17", "parity17"), "0.0.1", typeof(DataObject).Assembly);
services.AddSingleton<IArtifactGraphMaterializer, SpikeArtifactGraphMaterializer>();
var provider = services.BuildServiceProvider();
var operations = provider.GetRequiredService<IOperations>();

var sw = Stopwatch.StartNew();
Base root;
string leg = args[0];
string source;
long receiveMs;

if (leg == "legacy")
{
  var (serverUrl, projectId, rootId, outJson) = (args[1], args[2], args[3], args[4]);
  source = $"{serverUrl}/projects/{projectId} rootId={rootId}";
  Console.WriteLine($"[legacy] Receive2({rootId}) from {serverUrl} …");
  root = await operations.Receive2(new Uri(serverUrl), projectId, rootId, null, null, CancellationToken.None);
  receiveMs = sw.ElapsedMilliseconds;
  Console.WriteLine($"[legacy] received in {receiveMs} ms");
  Dump(root, "legacy", source, outJson, receiveMs);
  return 0;
}

if (leg == "bundle")
{
  var (bundleDir, projectId, modelId, versionId, outJson) = (args[1], args[2], args[3], args[4], args[5]);
  const string Token = "parity-token";
  string craftedId = $"{projectId};{modelId};{versionId}";
  source = $"bundleDir={bundleDir} craftedId={craftedId}";

  int port = 18177;
  var listener = new HttpListener();
  listener.Prefixes.Add($"http://localhost:{port}/");
  listener.Start();
  var cts = new CancellationTokenSource();
  _ = Task.Run(async () =>
  {
    while (!cts.IsCancellationRequested)
    {
      HttpListenerContext ctx;
      try
      {
        ctx = await listener.GetContextAsync();
      }
      catch (Exception) when (cts.IsCancellationRequested)
      {
        break;
      }
      string path = ctx.Request.Url!.AbsolutePath;
      if (path == $"/api/v2/projects/{projectId}/models/{modelId}/versions/{versionId}/artifacts")
      {
        if (ctx.Request.Headers["Authorization"] != $"Bearer {Token}")
        {
          ctx.Response.StatusCode = 401;
          ctx.Response.Close();
          continue;
        }
        var files = Directory
          .GetFiles(bundleDir)
          .Select(Path.GetFileName)
          .Select(n => $"{{\"name\":\"{n}\",\"url\":\"http://localhost:{port}/fake-s3/{n}?sig=presigned\"}}");
        byte[] body = Encoding.UTF8.GetBytes($"{{\"files\":[{string.Join(",", files)}]}}");
        ctx.Response.ContentType = "application/json";
        await ctx.Response.OutputStream.WriteAsync(body);
        ctx.Response.Close();
      }
      else if (path.StartsWith("/fake-s3/"))
      {
        string file = Path.Combine(bundleDir, Path.GetFileName(path));
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
        ctx.Response.StatusCode = 404;
        ctx.Response.Close();
      }
    }
  });

  Console.WriteLine($"[bundle] Receive2({craftedId}) via stub :{port} over {bundleDir} …");
  sw.Restart();
  root = await operations.Receive2(new Uri($"http://localhost:{port}"), projectId, craftedId, Token, null, CancellationToken.None);
  receiveMs = sw.ElapsedMilliseconds;
  Console.WriteLine($"[bundle] received in {receiveMs} ms");
  cts.Cancel();
  listener.Stop();
  Dump(root, "bundle", source, outJson, receiveMs);
  return 0;
}

Console.Error.WriteLine($"unknown leg '{leg}'");
return 2;

// ── walker/dump ──────────────────────────────────────────────────────────────────────────────────────────

static void Dump(Base root, string leg, string source, string outJson, long receiveMs)
{
  long gcBytes = GC.GetTotalMemory(forceFullCollection: true);
  long workingSet = Environment.WorkingSet;

  var elements = new List<Dictionary<string, object?>>();
  var typeCounts = new SortedDictionary<string, int>();
  var clrCounts = new SortedDictionary<string, int>();
  int collections = 0;
  long totalProps = 0;
  int sampled = 0;

  // root-level proxies / views (v3 graph inventory on the legacy side; whatever the reader emits on the bundle side)
  var rootExtras = new SortedDictionary<string, object?>();
  foreach (var m in root.GetMembers(DynamicBaseMemberType.Dynamic))
  {
    rootExtras[m.Key] = m.Value switch
    {
      System.Collections.ICollection c => $"count={c.Count}",
      null => null,
      _ => m.Value.GetType().Name,
    };
  }

  void Walk(Base b, string colPath, int depth)
  {
    string sType = b.speckle_type ?? "<null>";
    typeCounts[sType] = typeCounts.GetValueOrDefault(sType) + 1;
    string clr = b.GetType().FullName ?? "?";
    clrCounts[clr] = clrCounts.GetValueOrDefault(clr) + 1;

    if (b is Collection col)
    {
      collections++;
      string here = colPath.Length == 0 ? (col.name ?? "<root>") : $"{colPath}/{col.name}";
      foreach (var child in col.elements)
      {
        Walk(child, here, depth + 1);
      }
      return;
    }

    // element node
    var flat = new SortedDictionary<string, string?>(StringComparer.Ordinal);
    if (b is DataObject dobj)
    {
      FlattenDict(dobj.properties, "", flat, 0);
    }
    else
    {
      // typed/legacy element: flatten its dynamic members (covers v2-era `parameters` etc.)
      foreach (var m in b.GetMembers(DynamicBaseMemberType.Dynamic))
      {
        FlattenValue(m.Value, m.Key, flat, 0);
      }
    }
    totalProps += flat.Count;

    int dvMesh = 0;
    long dvVerts = 0;
    long dvFaceLen = 0;
    var dvOther = new SortedDictionary<string, int>();
    var dv = (b as DataObject)?.displayValue?.Cast<Base?>().ToList() ?? TryGetDisplay(b);
    foreach (var d in dv ?? new List<Base?>())
    {
      if (d is Mesh m2)
      {
        dvMesh++;
        dvVerts += m2.vertices.Count / 3;
        dvFaceLen += m2.faces.Count;
      }
      else if (d is not null)
      {
        dvOther[d.speckle_type] = dvOther.GetValueOrDefault(d.speckle_type) + 1;
      }
    }

    var rec = new Dictionary<string, object?>
    {
      ["a"] = b.applicationId,
      ["i"] = b.id,
      ["t"] = sType,
      ["n"] = (b as DataObject)?.name ?? (b["name"] as string),
      ["p"] = colPath,
      ["np"] = flat.Count,
      ["ph"] = HashProps(flat),
      ["pp"] = flat.Keys.ToArray(),
      ["dv"] = new object?[] { dvMesh, dvVerts, dvFaceLen, dvOther.Count == 0 ? null : dvOther },
    };
    // full values on a deterministic appId-keyed sample so BOTH legs sample the same elements
    if (sampled < 400 && b.applicationId is { } aid && (StableHash(aid) % 23) == 0)
    {
      rec["v"] = flat;
      sampled++;
    }
    elements.Add(rec);
  }

  Walk(root, "", 0);

  var payload = new Dictionary<string, object?>
  {
    ["leg"] = leg,
    ["source"] = source,
    ["receiveMs"] = receiveMs,
    ["workingSetMB"] = workingSet / (1024 * 1024),
    ["gcHeapMB"] = gcBytes / (1024 * 1024),
    ["rootType"] = root.speckle_type,
    ["rootName"] = (root as Collection)?.name,
    ["rootExtras"] = rootExtras,
    ["collections"] = collections,
    ["elementCount"] = elements.Count,
    ["totalFlattenedProps"] = totalProps,
    ["typeCounts"] = typeCounts,
    ["clrCounts"] = clrCounts,
    ["elements"] = elements,
  };
  File.WriteAllText(outJson, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = false }));
  Console.WriteLine(
    $"[{leg}] dumped {elements.Count} elements, {collections} collections, {totalProps} props "
      + $"→ {outJson} (ws {workingSet / (1024 * 1024)} MB, gc {gcBytes / (1024 * 1024)} MB)"
  );
}

static List<Base?>? TryGetDisplay(Base b)
{
  var raw = b["displayValue"] ?? b["@displayValue"];
  return raw switch
  {
    Base single => new List<Base?> { single },
    IEnumerable<Base?> many => many.ToList(),
    IEnumerable<object?> objs => objs.OfType<Base>().Cast<Base?>().ToList(),
    _ => null,
  };
}

static void FlattenDict(IReadOnlyDictionary<string, object?> d, string prefix, SortedDictionary<string, string?> into, int depth)
{
  foreach (var kv in d)
  {
    FlattenValue(kv.Value, prefix.Length == 0 ? kv.Key : $"{prefix}.{kv.Key}", into, depth);
  }
}

static void FlattenValue(object? v, string path, SortedDictionary<string, string?> into, int depth)
{
  if (depth > 14)
  {
    into[path] = "<depth-capped>";
    return;
  }
  switch (v)
  {
    case null:
      into[path] = null;
      break;
    case Dictionary<string, object?> dd:
      FlattenDict(dd, path, into, depth + 1);
      break;
    case IReadOnlyDictionary<string, object?> rd:
      FlattenDict(rd, path, into, depth + 1);
      break;
    case Base bb when path.EndsWith("displayValue"):
      into[path] = $"<{bb.speckle_type}>";
      break;
    case Base bb:
      into[path] = $"<{bb.speckle_type}>";
      break;
    case string s:
      into[path] = s;
      break;
    case bool bo:
      into[path] = bo ? "true" : "false";
      break;
    case sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal:
      into[path] = Canon(Convert.ToDouble(v, CultureInfo.InvariantCulture));
      break;
    case System.Collections.IEnumerable list:
      {
        int i = 0;
        int scalarish = 0;
        foreach (var item in list)
        {
          if (item is Dictionary<string, object?> or IReadOnlyDictionary<string, object?> or Base)
          {
            FlattenValue(item, $"{path}[{i}]", into, depth + 1);
          }
          else
          {
            scalarish++;
          }
          i++;
        }
        if (scalarish > 0)
        {
          into[path] = $"<list:{i} items>";
        }
        break;
      }
    default:
      into[path] = v.ToString();
      break;
  }
}

static string Canon(double d)
{
  // canonical numeric form so int-vs-double round-trips hash identically across rails
  if (double.IsNaN(d) || double.IsInfinity(d))
  {
    return d.ToString(CultureInfo.InvariantCulture);
  }
  double r = Math.Round(d, 9);
  return r == Math.Floor(r) && Math.Abs(r) < 1e15
    ? ((long)r).ToString(CultureInfo.InvariantCulture)
    : r.ToString("G15", CultureInfo.InvariantCulture);
}

static int StableHash(string s)
{
  unchecked
  {
    int h = 23;
    foreach (char c in s)
    {
      h = h * 31 + c;
    }
    return h & 0x7fffffff;
  }
}

static string HashProps(SortedDictionary<string, string?> flat)
{
  var sb = new StringBuilder();
  foreach (var kv in flat)
  {
    sb.Append(kv.Key).Append('=').Append(kv.Value ?? "\0null").Append('\n');
  }
  return Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(sb.ToString())))[..16];
}
