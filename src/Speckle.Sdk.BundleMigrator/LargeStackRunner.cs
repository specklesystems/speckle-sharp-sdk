namespace Speckle.Sdk.BundleMigrator;

/// <summary>
/// Runs work on a dedicated thread with a large stack reservation. The legacy deserializer recurses one stack
/// level per level of detached-object nesting, and every await on the packfile path completes synchronously, so a
/// deep v2 tree overflows the ~1 MB default of a pool thread. The reservation is virtual — pages are committed only
/// as the recursion actually uses them.
/// </summary>
internal static class LargeStackRunner
{
  public const int StackSize = 512 * 1024 * 1024;

  /// <summary>
  /// The work stays on the large-stack thread only while its awaits complete synchronously; a genuinely
  /// asynchronous await resumes on the pool with a default stack.
  /// </summary>
  public static Task<T> Run<T>(Func<Task<T>> work, string threadName)
  {
    var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
    var thread = new Thread(
      () =>
      {
        try
        {
          tcs.SetResult(work().GetAwaiter().GetResult());
        }
        catch (OperationCanceledException oce)
        {
          tcs.SetCanceled(oce.CancellationToken);
        }
#pragma warning disable CA1031 // marshalled to the awaiting caller, not swallowed
        catch (Exception ex)
#pragma warning restore CA1031
        {
          tcs.SetException(ex);
        }
      },
      StackSize
    )
    {
      IsBackground = true,
      Name = threadName,
    };
    thread.Start();
    return tcs.Task;
  }
}
