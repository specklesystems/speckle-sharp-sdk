using Speckle.Sdk.Transports;

namespace Speckle.Sdk.BundleMigrator.Transports;

/// <summary>
/// Fans reads across <paramref name="readTransports"/> in order (first hit wins) and writes to every
/// transport in <paramref name="writeTransports"/>.
///
/// Read transports must all be LOCAL. <see cref="ServerTransport.CopyObjectAndChildren"/> decides what to
/// download by asking its target which ids it already has, so an aggregate that can reach the server
/// answers "all of them" and nothing gets fetched.
/// </summary>
internal sealed class AggregateTransport(
  IReadOnlyList<ITransport> readTransports,
  IReadOnlyList<ITransport> writeTransports
) : ITransport
{
  public string TransportName { get; set; } = nameof(AggregateTransport);
  public Dictionary<string, object> TransportContext { get; } = new();
  public TimeSpan Elapsed { get; } = TimeSpan.Zero;
  public IProgress<ProgressArgs>? OnProgressAction { get; set; }

  private CancellationToken _cancellationToken;

  public CancellationToken CancellationToken
  {
    get => _cancellationToken;
    set
    {
      _cancellationToken = value;
      // Children read their own token; storing it here alone would silently disable cancellation.
      foreach (var t in readTransports.Concat(writeTransports).Distinct())
      {
        t.CancellationToken = value;
      }
    }
  }

  public void BeginWrite()
  {
    foreach (var t in writeTransports)
    {
      t.BeginWrite();
    }
  }

  public void EndWrite()
  {
    foreach (var t in writeTransports)
    {
      t.EndWrite();
    }
  }

  public void SaveObject(string id, string serializedObject)
  {
    foreach (var t in writeTransports)
    {
      t.SaveObject(id, serializedObject);
    }
  }

  public async Task WriteComplete()
  {
    foreach (var t in writeTransports)
    {
      await t.WriteComplete().ConfigureAwait(false);
    }
  }

  public async Task<string?> GetObject(string id)
  {
    foreach (var t in readTransports)
    {
      string? o = await t.GetObject(id).ConfigureAwait(false);
      if (o is not null)
      {
        return o;
      }
    }

    return null;
  }

  public async Task<Dictionary<string, bool>> HasObjects(IReadOnlyList<string> objectIds)
  {
    Dictionary<string, bool> ret = new(objectIds.Count);
    foreach (string objectId in objectIds)
    {
      ret[objectId] = false;
    }

    List<string> toCheck = ret.Keys.ToList();
    foreach (var t in readTransports)
    {
      if (toCheck.Count == 0)
      {
        break;
      }

      var has = await t.HasObjects(toCheck).ConfigureAwait(false);
      foreach (var o in has)
      {
        if (o.Value)
        {
          ret[o.Key] = true;
        }
      }

      toCheck = toCheck.Where(id => !ret[id]).ToList();
    }

    return ret;
  }

  /// <summary>Not supported — the direction is reversed. Call it on the SOURCE transport, passing this
  /// aggregate as the target.</summary>
  public Task<string> CopyObjectAndChildren(string id, ITransport targetTransport) =>
    throw new NotSupportedException(
      $"Copy INTO an {nameof(AggregateTransport)}, not out of one: call {nameof(CopyObjectAndChildren)} on the source transport with this aggregate as the target."
    );
}
