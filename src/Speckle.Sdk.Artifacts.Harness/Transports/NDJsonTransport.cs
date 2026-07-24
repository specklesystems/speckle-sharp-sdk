using System.IO.Compression;
using System.Text.RegularExpressions;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Artifacts.Harness.Transports;

/// <summary>
/// An <see cref="ITransport"/> backed by an in-memory store populated from a harness NDJSON dump
/// (tab-separated lines, the payload json being the last field). Reads delegate to an inner
/// <see cref="MemoryTransport"/>; call <see cref="Initialize"/> to load the file before deserializing.
/// </summary>
internal sealed partial class NDJsonTransport : ITransport
{
  private readonly MemoryTransport _memory = new();

  /// <summary>Copies every object in <paramref name="ndjson"/> into the in-memory store; returns the count loaded.</summary>
  public int Initialize(FileInfo ndjson)
  {
    var count = 0;
    foreach (var line in ReadLines(ndjson))
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
      _memory.SaveObject(parts[0], parts[^1]); // id, and the payload json is always the last field
      count++;
    }
    return count;
  }

  /// <summary>Best-effort root id: prefer an unreferenced object, then a collection-like one, then the largest.</summary>
  public string? DetectRoot()
  {
    var referenced = new HashSet<string>(StringComparer.Ordinal);
    foreach (var json in _memory.Objects.Values)
    {
      foreach (Match m in ReferenceIdRegex().Matches(json))
      {
        referenced.Add(m.Groups[1].Value);
      }
    }
    var unreferenced = _memory.Objects.Keys.Where(id => !referenced.Contains(id)).ToList();
    var pool = unreferenced.Count > 0 ? unreferenced : _memory.Objects.Keys.ToList();
    return pool.OrderByDescending(id => LooksLikeCollection(_memory.Objects[id]) ? 1 : 0)
      .ThenByDescending(id => _memory.Objects[id].Length)
      .FirstOrDefault();
  }

  public IEnumerable<string> CollectionCandidates() =>
    _memory
      .Objects.Where(kv => LooksLikeCollection(kv.Value))
      .OrderByDescending(kv => kv.Value.Length)
      .Select(kv => kv.Key);

  private static bool LooksLikeCollection(string json) =>
    json.Contains("Collection", StringComparison.Ordinal)
    || json.Contains("instanceDefinitionProxies", StringComparison.Ordinal)
    || json.Contains("renderMaterialProxies", StringComparison.Ordinal);

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

  public string TransportName
  {
    get => _memory.TransportName;
    set => _memory.TransportName = value;
  }
  public Dictionary<string, object> TransportContext => _memory.TransportContext;
  public TimeSpan Elapsed => _memory.Elapsed;
  public CancellationToken CancellationToken
  {
    get => _memory.CancellationToken;
    set => _memory.CancellationToken = value;
  }
  public IProgress<ProgressArgs>? OnProgressAction
  {
    get => _memory.OnProgressAction;
    set => _memory.OnProgressAction = value;
  }

  public Task<string?> GetObject(string id) => _memory.GetObject(id);

  public Task<Dictionary<string, bool>> HasObjects(IReadOnlyList<string> objectIds) => _memory.HasObjects(objectIds);

  #region Writes (not implemented)
  public Task<string> CopyObjectAndChildren(string id, ITransport targetTransport) =>
    throw new NotImplementedException();

  public Task WriteComplete() => throw new NotImplementedException();

  public void BeginWrite() => throw new NotImplementedException();

  public void EndWrite() => throw new NotImplementedException();

  public void SaveObject(string id, string serializedObject) => throw new NotImplementedException();

  [GeneratedRegex("\"referencedId\":\"([0-9a-fA-F]+)\"")]
  private static partial Regex ReferenceIdRegex();
  #endregion
}
