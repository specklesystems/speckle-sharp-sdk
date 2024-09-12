using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Api;

/// <summary>
/// Exposes several key methods for interacting with Speckle.Sdk.
/// <para>Serialize/Deserialize</para>
/// <para>Push/Pull (methods to serialize and send data to one or more servers)</para>
/// </summary>
public partial class Operations(ILogger<Operations> logger, ILoggerFactory loggerFactory)
{
  /// <summary>
  /// Factory for progress actions used internally inside send and receive methods.
  /// </summary>
  /// <param name="onProgressAction"></param>
  /// <returns></returns>
  private static Action<ProgressArgs>? GetInternalProgressAction(Action<ConcurrentBag<ProgressArgs>>? onProgressAction)
  {
    if (onProgressAction is null)
    {
      return null;
    }

    return (args) =>
    {
      var localProgressDict = new ConcurrentBag<ProgressArgs>();
      localProgressDict.Add(args);

      onProgressAction.Invoke(localProgressDict);
    };
  }
}
