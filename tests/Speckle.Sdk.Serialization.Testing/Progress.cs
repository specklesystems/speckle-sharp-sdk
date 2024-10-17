using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialization.Testing;

public class Progress(bool write) : IProgress<ProgressArgs>
{
  private readonly TimeSpan DEBOUNCE = TimeSpan.FromMilliseconds(500);
  private DateTime _lastTime = DateTime.UtcNow;

  public void Report(ProgressArgs value)
  {
    if (write)
    {
      var now = DateTime.UtcNow;
      if (now - _lastTime >= DEBOUNCE)
      {
        Console.WriteLine(
          value.ProgressEvent + " " + value.Count + " " + value.Total + " " + Environment.CurrentManagedThreadId
        );
        _lastTime = now;
      }
    }
  }
}
