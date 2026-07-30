using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Artifacts.Harness.Transports;

internal class AggregateTransport(IReadOnlyList<ITransport> ReadTransports, IReadOnlyList<ITransport> WriteTransports)
  : ITransport
{
  public string TransportName { get; set; } = nameof(AggregateTransport);
  public Dictionary<string, object> TransportContext { get; } = new();
  public TimeSpan Elapsed { get; } = TimeSpan.Zero;
  public CancellationToken CancellationToken { get; set; }
  public IProgress<ProgressArgs>? OnProgressAction { get; set; }

  public void BeginWrite()
  {
    foreach (var t in WriteTransports)
    {
      t.BeginWrite();
    }
  }

  public void EndWrite()
  {
    foreach (var t in WriteTransports)
    {
      t.EndWrite();
    }
  }

  public void SaveObject(string id, string serializedObject)
  {
    foreach (var t in WriteTransports)
    {
      t.SaveObject(id, serializedObject);
    }
  }

  public async Task WriteComplete()
  {
    foreach (var t in WriteTransports)
    {
      await t.WriteComplete();
    }
  }

  public async Task<string?> GetObject(string id)
  {
    foreach (var t in ReadTransports)
    {
      string? o = await t.GetObject(id);
      if (o is not null)
      {
        return o;
      }
    }

    return null;
  }

  public async Task<string> CopyObjectAndChildren(string id, ITransport targetTransport)
  {
    foreach (var t in ReadTransports)
    {
      string? o = await t.GetObject(id);
      if (o is not null)
      {
        await t.CopyObjectAndChildren(id, targetTransport);
      }
    }
    throw new TransportException($"Requested id {id} was not found within any transports");
  }

  public async Task<Dictionary<string, bool>> HasObjects(IReadOnlyList<string> objectIds)
  {
    Dictionary<string, bool> ret = new(objectIds.Count);
    foreach (string objectId in objectIds)
    {
      ret[objectId] = false;
    }

    List<string> toCheck = objectIds.ToList();

    foreach (var t in ReadTransports)
    {
      var has = await t.HasObjects(toCheck.ToList());

      foreach (var o in has)
      {
        if (o.Value)
        {
          ret[o.Key] = true;
          toCheck.Remove(o.Key);
        }
      }

      toCheck = ret.Where(kvp => !kvp.Value).Select(kvp => kvp.Key).ToList();
    }

    return ret;
  }
}
