using System.Collections.Concurrent;

namespace Speckle.Sdk.Api;

/// <summary>
/// Exposes several key methods for interacting with Speckle.Sdk.
/// <para>Serialize/Deserialize</para>
/// <para>Push/Pull (methods to serialize and send data to one or more servers)</para>
/// </summary>
public static partial class Operations
{
  /// <summary>
  /// Factory for progress actions used internally inside send and receive methods.
  /// </summary>
  /// <param name="onProgressAction"></param>
  /// <returns></returns>
  private static Action<string, int>? GetInternalProgressAction(
    Action<ConcurrentDictionary<string, int>>? onProgressAction
  )
  {
    if (onProgressAction is null)
    {
      return null;
    }

    var localProgressDict = new ConcurrentDictionary<string, int>();

    return (name, processed) =>
    {
      if (!localProgressDict.TryAdd(name, processed))
      {
        localProgressDict[name] += processed;
      }

      onProgressAction.Invoke(localProgressDict);
    };
  }
}
