#if NETSTANDARD2_0 || NET8_0_OR_GREATER
using System.Collections.Concurrent;

namespace Speckle.Sdk.Pipelines.Send.Artifacts;

/// <summary>
/// <para><b>Why this exists — the deadlock.</b> The extraction runs under ODA's
/// <c>ThreadPoolPinnedScheduler</c> (a single-thread <see cref="System.Threading.Tasks.TaskScheduler"/>).
/// On that thread <c>TaskScheduler.Current</c> is the pinned scheduler, so when a parquet write
/// <c>await</c>s real disk IO its continuation is posted back to the pinned scheduler's queue — but
/// that thread is blocked inside <c>.GetResult()</c> waiting for the write, so it can never run the
/// continuation. Write waits for the thread; thread waits for the write → hang (only at scale, when a
/// flush's IO actually yields). This thread is a plain <see cref="Thread"/> with no such scheduler, so
/// the continuation resumes on the thread pool and completes. The producer never blocks on async IO.</para>
///
/// <para><b>Two more wins.</b> Writing now overlaps extraction (the ODA thread tessellates the next
/// element while this thread flushes the previous batch), and the BOUNDED queue applies backpressure —
/// when the writer falls behind, <see cref="Enqueue"/> blocks the producer, so in-flight row groups
/// (hence memory) stay bounded and the process can't balloon into swap.</para>
///
/// One thread, FIFO: a file's row groups and its finalize run in order; different files serialize
/// (Parquet wants per-file sequential writes anyway). Not the place for cross-file IO parallelism —
/// that would be a multi-consumer follow-up.
/// </summary>
public sealed class ParquetWriteScheduler : IDisposable
{
  /// <summary>Max row groups in flight (across all files). Bounds memory; blocks the producer when full.</summary>
  public const string CAPACITY_ENV_VAR = "SPECKLE_PARQUET_WRITE_QUEUE";
  private const int DEFAULT_CAPACITY = 4;

  private readonly BlockingCollection<Action> _jobs;
  private readonly Thread _thread;
  private volatile Exception? _fault;

  public ParquetWriteScheduler()
  {
    _jobs = new BlockingCollection<Action>(ResolveCapacity());
    _thread = new Thread(Pump) { IsBackground = true, Name = "speckle-parquet-writer" };
    _thread.Start();
  }

  /// <summary>
  /// Hands one already-snapshotted row-group (or finalize) job to the background thread. Blocks the
  /// caller while the bounded queue is full — that IS the backpressure. Re-throws on the caller's
  /// thread if a prior job faulted, so a write failure stops the producer instead of being lost.
  /// </summary>
  public void Enqueue(Action job)
  {
    ThrowIfFaulted();
    try
    {
      _jobs.Add(job);
    }
    catch (InvalidOperationException) when (_jobs.IsAddingCompleted)
    {
      // The pump completed the collection (fault, or someone enqueued after CompleteAndWait).
      ThrowIfFaulted(); // prefer the real cause if there is one
      throw;
    }
  }

  /// <summary>
  /// Signals no more jobs, drains the queue (every file flushed + closed), then re-throws any
  /// background fault. Call once, after every writer's <c>Complete()</c> has enqueued its final
  /// flush + finalize — this is the barrier the uploader relies on (files are on disk after it returns).
  /// </summary>
  public void CompleteAndWait()
  {
    if (!_jobs.IsAddingCompleted)
    {
      _jobs.CompleteAdding();
    }
    _thread.Join();
    ThrowIfFaulted();
  }

  private void Pump()
  {
    foreach (var job in _jobs.GetConsumingEnumerable())
    {
      if (_fault is not null)
      {
        continue; // drain the rest without executing, so producers parked on Add() unblock
      }
      try
      {
        job();
      }
#pragma warning disable CA1031 // capture ANY job failure to re-throw on the producer thread
      catch (Exception ex)
#pragma warning restore CA1031
      {
        _fault = ex;
        _jobs.CompleteAdding(); // stop accepting + unblock a producer blocked in Add()
      }
    }
  }

  private void ThrowIfFaulted()
  {
    var fault = _fault;
    if (fault is not null)
    {
      throw new InvalidOperationException("A background parquet row-group write failed.", fault);
    }
  }

  public void Dispose()
  {
    if (!_jobs.IsAddingCompleted)
    {
      _jobs.CompleteAdding();
    }
    if (_thread.IsAlive)
    {
      _thread.Join();
    }
    _jobs.Dispose();
  }

  private static int ResolveCapacity()
  {
    var raw = Environment.GetEnvironmentVariable(CAPACITY_ENV_VAR);
    return int.TryParse(raw, out var n) && n > 0 ? n : DEFAULT_CAPACITY;
  }
}
#endif
