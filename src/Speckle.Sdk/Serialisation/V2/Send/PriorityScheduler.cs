using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Speckle.Sdk.Serialisation.V2.Send;

public sealed class PriorityScheduler(
  ILogger<PriorityScheduler> logger,
  ThreadPriority priority,
  int maximumConcurrencyLevel,
  CancellationToken cancellationToken
) : TaskScheduler, IDisposable
{
  private readonly BlockingCollection<Task> _tasks = new();
  private Thread[]? _threads;

  public void Dispose()
  {
    _tasks.CompleteAdding();
    _tasks.Dispose();
  }

  public override int MaximumConcurrencyLevel => maximumConcurrencyLevel;

  protected override IEnumerable<Task> GetScheduledTasks() => _tasks;

  protected override void QueueTask(Task task)
  {
    _tasks.Add(task);

    if (_threads == null)
    {
      _threads = new Thread[maximumConcurrencyLevel];
      for (int i = 0; i < _threads.Length; i++)
      {
        _threads[i] = new Thread(() =>
        {
          try
          {
            foreach (Task t in _tasks.GetConsumingEnumerable(cancellationToken))
            {
              if (cancellationToken.IsCancellationRequested)
              {
                break;
              }

              TryExecuteTask(t);
              if (cancellationToken.IsCancellationRequested)
              {
                break;
              }
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
  }

  protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => false; // we might not want to execute task that should schedule as high or low priority inline
}
