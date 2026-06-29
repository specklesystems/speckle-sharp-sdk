#if NETSTANDARD2_0 || NET8_0_OR_GREATER
using System.Diagnostics;

namespace Speckle.Sdk.Pipelines.Send.Artifacts;

/// <summary>
/// Whole-run memory profiler for import benchmarking (grep "[mem").
/// A background thread samples the process every 100ms from first use; callers
/// mark phase boundaries with <see cref="Phase"/> (oda: model load, extraction
/// loop… / sdk: index builds, uploads…) and <see cref="Report"/> prints a
/// per-phase table — duration, working-set start→end, peak working set, peak
/// managed, peak native — plus the global peak and which phase owned it.
/// <para>
/// Reading the numbers: <c>workingSet</c> is physical RAM as a container
/// memory limit sees it; <c>managed</c> is the live .NET heap; the gap between
/// workingSet and <c>gcCommitted</c> is NATIVE memory (ODA, DuckDB, allocator
/// retention) — invisible to the GC but fatal to a pod limit all the same.
/// </para>
/// Set <c>SPECKLE_MEMORY_CSV=/path/run.csv</c> to also dump every sample
/// (t, phase, managed, gcCommitted, workingSet) for charting.
/// </summary>
public static class MemoryLog
{
  private const double MB = 1024 * 1024;
  private const int SAMPLE_INTERVAL_MS = 100;

  private static readonly object s_lock = new();
  private static readonly Stopwatch s_clock = Stopwatch.StartNew();
  private static readonly List<Sample> s_samples = [];
  private static string s_phase = "startup";
  private static bool s_started;
  private static bool s_stopped;

  private readonly record struct Sample(double T, string Phase, long Managed, long Committed, long WorkingSet);

  /// <summary>Switch the current phase; subsequent samples are attributed to it.</summary>
  public static void Phase(string name)
  {
    Sample sample;
    lock (s_lock)
    {
      EnsureSampler();
      TakeSampleLocked(); // boundary sample closes out the previous phase
      s_phase = name;
      sample = TakeSampleLocked();
    }
    Emit(name, sample);
  }

  /// <summary>Checkpoint line without switching phases.</summary>
  public static void Log(string label)
  {
    Sample sample;
    lock (s_lock)
    {
      EnsureSampler();
      sample = TakeSampleLocked();
    }
    Emit(label, sample);
  }

  /// <summary>Stops sampling and prints the per-phase report (+ CSV if SPECKLE_MEMORY_CSV is set).</summary>
  public static void Report()
  {
    Sample[] samples;
    lock (s_lock)
    {
      EnsureSampler();
      TakeSampleLocked();
      s_stopped = true;
      samples = [.. s_samples];
    }

    Console.WriteLine();
    Console.WriteLine("[mem] ================== memory report ==================");
    Console.WriteLine("[mem] workingSet = physical RAM (what a pod memory limit enforces); native = workingSet - gcCommitted");
    Console.WriteLine(
      "[mem] phase                                          dur     workingSet        peakWS  peakManaged  peakNative"
    );

    var globalPeak = samples[0];
    var i = 0;
    while (i < samples.Length)
    {
      var phase = samples[i].Phase;
      long peakWs = 0,
        peakManaged = 0,
        peakNative = 0;
      var j = i;
      while (j < samples.Length && samples[j].Phase == phase)
      {
        var s = samples[j];
        peakWs = Math.Max(peakWs, s.WorkingSet);
        peakManaged = Math.Max(peakManaged, s.Managed);
        peakNative = Math.Max(peakNative, s.WorkingSet - s.Committed);
        if (s.WorkingSet > globalPeak.WorkingSet)
        {
          globalPeak = s;
        }
        j++;
      }
      var first = samples[i];
      var last = samples[j - 1];
      Console.WriteLine(
        FormattableString.Invariant(
          $"[mem] {phase,-44} {last.T - first.T,6:F1}s {first.WorkingSet / MB,6:F0}→{last.WorkingSet / MB,-6:F0}MB {peakWs / MB,6:F0}MB {peakManaged / MB,9:F0}MB {peakNative / MB,8:F0}MB"
        )
      );
      i = j;
    }

    Console.WriteLine(
      FormattableString.Invariant(
        $"[mem] GLOBAL PEAK workingSet {globalPeak.WorkingSet / MB:F0}MB at t={globalPeak.T:F1}s during '{globalPeak.Phase}'"
      )
    );

    var csvPath = Environment.GetEnvironmentVariable("SPECKLE_MEMORY_CSV");
    if (!string.IsNullOrWhiteSpace(csvPath))
    {
      var lines = new List<string>(samples.Length + 1) { "t_seconds,phase,managed_mb,gc_committed_mb,working_set_mb" };
      lines.AddRange(
        samples.Select(s =>
          FormattableString.Invariant(
            $"{s.T:F2},{s.Phase},{s.Managed / MB:F1},{s.Committed / MB:F1},{s.WorkingSet / MB:F1}"
          )
        )
      );
      File.WriteAllLines(csvPath, lines);
      Console.WriteLine($"[mem] {samples.Length} samples written to {csvPath}");
    }
  }

  private static void EnsureSampler()
  {
    if (s_started)
    {
      return;
    }
    s_started = true;
    var thread = new Thread(SampleLoop) { IsBackground = true, Name = "speckle-memlog" };
    thread.Start();
  }

  private static void SampleLoop()
  {
    while (true)
    {
      lock (s_lock)
      {
        if (s_stopped)
        {
          return;
        }
        TakeSampleLocked();
      }
      Thread.Sleep(SAMPLE_INTERVAL_MS);
    }
  }

  private static Sample TakeSampleLocked()
  {
    using var process = Process.GetCurrentProcess();
    var sample = new Sample(
      s_clock.Elapsed.TotalSeconds,
      s_phase,
      GC.GetTotalMemory(false),
#if NET8_0_OR_GREATER
      GC.GetGCMemoryInfo().TotalCommittedBytes,
#else
      0L, // GC.GetGCMemoryInfo() is core-only; this diagnostic field is unavailable on netstandard2.0
#endif
      process.WorkingSet64
    );
    s_samples.Add(sample);
    return sample;
  }

  private static void Emit(string label, Sample s) =>
    Console.WriteLine(
      FormattableString.Invariant(
        $"[mem {s.T,7:F1}s] {label,-44} managed={s.Managed / MB,6:F0}MB gcCommitted={s.Committed / MB,6:F0}MB workingSet={s.WorkingSet / MB,6:F0}MB"
      )
    );
}
#endif
