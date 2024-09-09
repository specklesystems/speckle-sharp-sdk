using System.Collections.Concurrent;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Serialisation.Receive;

public class CachingStage
{
  private readonly ConcurrentDictionary<string, Base> _idToBaseCache;

  public CachingStage(ConcurrentDictionary<string, Base> idToBaseCache)
  {
    _idToBaseCache = idToBaseCache;
  }

  public IReadOnlyDictionary<string, Base> Cache => _idToBaseCache;
}
