using System.Diagnostics;

namespace Speckle.Core.Logging;

public class CumulativeTimer
{
  internal class Operation : IDisposable
  {
    private static readonly double s_stopwatchToTimeSpanTicks = Stopwatch.Frequency / 10000000.0;

    private readonly CumulativeTimer _cumulativeTimer;
    private readonly string _operationName;
    private readonly long _start;
    private long? _stop;

    internal Operation(CumulativeTimer cumulativeTimer, string operationName)
    {
      _cumulativeTimer = cumulativeTimer;
      _operationName = operationName;
      _start = GetTimestamp();
    }

    public TimeSpan Elapsed
    {
      get
      {
        long num = (_stop ?? GetTimestamp()) - _start;
        if (num < 0)
        {
          return TimeSpan.Zero;
        }

        return TimeSpan.FromTicks(num);
      }
    }

    private static long GetTimestamp()
    {
      return (long)(Stopwatch.GetTimestamp() / s_stopwatchToTimeSpanTicks);
    }

    private void StopTiming()
    {
      if (!_stop.HasValue)
      {
        _stop = GetTimestamp();
      }
    }

    public void Dispose()
    {
      StopTiming();
      _cumulativeTimer.AddTimer(_operationName, Elapsed);
    }
  }

  private readonly Dictionary<string, TimeSpan> _operationTimings = new();

  public void AddTimer(string operationName, TimeSpan time)
  {
    if (_operationTimings.TryGetValue(operationName, out TimeSpan prevTimings))
    {
      _operationTimings[operationName] = prevTimings + time;
    }
    else
    {
      _operationTimings.Add(operationName, time);
    }
  }
}
