using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Speckle.Sdk.Serialisation.V2;

public sealed class PriorityScheduler(
  ILogger<PriorityScheduler> logger,
  ThreadPriority priority,
  int maximumConcurrencyLevel,
  CancellationToken cancellationToken
) : TaskScheduler, IDisposable
{
#pragma warning disable CA2213
  //intentionally not disposing this because syncing to when all the threads are done AFTER the BC is done/disposed is hard.  BC will still be cleaned up by the finalizer
  private readonly BlockingCollection<Task> _tasks = new();
#pragma warning restore CA2213
  private Thread[]? _threads;

  public void Dispose() => _tasks.CompleteAdding();

  public override int MaximumConcurrencyLevel => maximumConcurrencyLevel;

  protected override IEnumerable<Task> GetScheduledTasks() => _tasks;

  protected override void QueueTask(Task task)
  {
    _tasks.Add(task);

    if (_threads != null)
    {
      return;
    }

    _threads = new Thread[maximumConcurrencyLevel];
    for (int i = 0; i < _threads.Length; i++)
    {
      _threads[i] = new Thread(() =>
      {
        try
        {
          while (true)
          {
            //we're done so leave
            if (_tasks.IsCompleted || cancellationToken.IsCancellationRequested)
            {
              break;
            }
            var success = _tasks.TryTake(out var t, TimeSpan.FromSeconds(1));
            //no task and we're done so leave
            if (success && _tasks.IsCompleted)
            {
              break;
            }
            //cancelled just leave
            if (cancellationToken.IsCancellationRequested)
            {
              break;
            }
            //didn't get a task but just timed out so continue
            if (!success)
            {
              continue;
            }
            TryExecuteTask(t ?? throw new InvalidOperationException("Task was null"));
          }
        }
        catch (OperationCanceledException)
        {
          //cancelling so end thread
        }
#pragma warning disable CA1031
        catch (Exception e)
#pragma warning restore CA1031
        {
          logger.LogError(e, "{name} had an exception", Thread.CurrentThread.Name);
        }
      })
      {
        Name = $"{priority}: {i}",
        Priority = priority,
        IsBackground = true,
      };
      _threads[i].Start();
    }
  }

  protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => false; // we might not want to execute task that should schedule as high or low priority inline

  public Task WaitForCompletion()
  {
    _tasks.CompleteAdding();
    return Task
      .Factory.StartNew(
        async () =>
        {
          while (_threads != null && _threads.Any(x => x.IsAlive))
          {
            await Task.Delay(TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);
          }
        },
        CancellationToken.None,
        TaskCreationOptions.AttachedToParent,
        TaskScheduler.Default
      )
      .Unwrap();
  }
}
