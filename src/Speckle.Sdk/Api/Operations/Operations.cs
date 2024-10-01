using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Api;

/// <summary>
/// Exposes several key methods for interacting with Speckle.Sdk.
/// <para>Serialize/Deserialize</para>
/// <para>Push/Pull (methods to serialize and send data to one or more servers)</para>
/// </summary>
[GenerateAutoInterface]
public partial class Operations(ILogger<Operations> logger, ISdkActivityFactory activityFactory) : IOperations
{
  /// <summary>
  /// Factory for progress actions used internally inside send and receive methods.
  /// </summary>
  /// <param name="onProgressAction"></param>
  /// <returns></returns>
  private static Func<ProgressArgs, Task>? GetInternalProgressAction(Func<ConcurrentBag<ProgressArgs>, Task>? onProgressAction)
  {
    if (onProgressAction is null)
    {
      return null;
    }

    return async (args) =>
    {
      var localProgressDict = new ConcurrentBag<ProgressArgs>();
      localProgressDict.Add(args);
        await onProgressAction.Invoke(localProgressDict).ConfigureAwait(false);
    };
  }
}
