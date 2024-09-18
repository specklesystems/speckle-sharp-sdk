namespace Speckle.Sdk.Serialisation.Receive;

public sealed class CachingStage(
  SqliteManagerOptions options,
  Func<string, CancellationToken, ValueTask> notCached,
  Func<Downloaded, CancellationToken, ValueTask> cached
) : IDisposable
{
  private readonly SqliteManager _sqLiteManager = new(options);

  public async ValueTask Execute(IReadOnlyList<string> ids, CancellationToken cancellationToken)
  {
    foreach (var (id, json) in _sqLiteManager.GetObjects(ids, cancellationToken))
    {
      if (json is null)
      {
        await notCached(id, cancellationToken).ConfigureAwait(false);
      }
      else
      {
        await cached(new(id, json), cancellationToken).ConfigureAwait(false);
      }
    }
  }

  public void Dispose() => _sqLiteManager.Dispose();
}
