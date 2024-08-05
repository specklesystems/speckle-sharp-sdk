using Speckle.Core.Logging;

namespace Speckle.Core.Transports;

public static class Utilities
{
  /// <summary>
  /// Waits until the provided function returns true.
  /// </summary>
  /// <param name="condition"></param>
  /// <param name="frequency"></param>
  /// <returns></returns>
  public static async Task WaitUntil(Func<bool> condition, int frequency = 25)
  {
      while (!condition())
      {
        await Task.Delay(frequency).ConfigureAwait(false);
      }
  }
}
