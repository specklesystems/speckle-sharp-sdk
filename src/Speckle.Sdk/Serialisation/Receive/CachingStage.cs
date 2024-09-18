namespace Speckle.Sdk.Serialisation.Receive;

public class CachingStage
{
  private readonly Func<string, ValueTask> _notCached;
  private readonly Func<Downloaded, ValueTask> _cached;
  private readonly SQLiteManager _sqLiteManager;

  public CachingStage(Func<string, ValueTask> notCached, Func<Downloaded, ValueTask> cached)
  {
    _notCached = notCached;
    _cached = cached;
    _sqLiteManager = new();
  }

  public async ValueTask Execute(IReadOnlyList<string> ids, CancellationToken cancellationToken)
  {
    foreach (var (id, json) in _sqLiteManager.GetObjects(ids, cancellationToken))
    {
      if (json is null)
      {
        await _notCached(id).ConfigureAwait(false);
      }
      else
      {
        await _cached(new(id, json)).ConfigureAwait(false);
      }
    }
  }
}
