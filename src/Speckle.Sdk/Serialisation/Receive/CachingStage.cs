using System.Collections.Concurrent;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Serialisation.Receive;

public class CachingStage(ConcurrentDictionary<string, Base> idToBaseCache)
{
  public IReadOnlyDictionary<string, Base> Cache => idToBaseCache;
}
