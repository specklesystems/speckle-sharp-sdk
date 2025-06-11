using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.SQLite;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation.V2.Send;

public partial interface IObjectSaverFactory : IDisposable;

[GenerateAutoInterface]
public sealed class ObjectSaverFactory(ILoggerFactory loggerFactory) : IObjectSaverFactory
{
  private readonly ConcurrentDictionary<string, IObjectSaver> _savers = new();

  public IObjectSaver Create(
    IServerObjectManager serverObjectManager,
    ISqLiteJsonCacheManager sqLiteJsonCacheManager,
    IProgress<ProgressArgs>? progress,
    CancellationToken cancellationToken,
    SerializeProcessOptions? options = null
  )
  {
    if (!_savers.TryGetValue(sqLiteJsonCacheManager.Path, out var saver))
    {
      saver = new ObjectSaver(
        progress,
        sqLiteJsonCacheManager,
        serverObjectManager,
        loggerFactory.CreateLogger<ObjectSaver>(),
        cancellationToken,
        options
      );
      _savers.TryAdd(sqLiteJsonCacheManager.Path, saver);
    }

    return saver;
  }

  [AutoInterfaceIgnore]
  public void Dispose()
  {
    foreach (var pool in _savers)
    {
      pool.Value.Dispose();
    }

    _savers.Clear();
  }
}
