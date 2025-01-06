using System.Collections.Concurrent;

namespace Speckle.Sdk.Serialisation.V2.Send;

public sealed class PriorityScheduler(ThreadPriority priority, int maximumConcurrencyLevel) : TaskScheduler, IDisposable
{
  private readonly CancellationTokenSource _cancellationTokenSource = new();
  private readonly BlockingCollection<Task> _tasks = new();
  private Thread[]? _threads;

  public void Dispose()
  {
    _tasks.CompleteAdding();
    _cancellationTokenSource.Cancel();
    _tasks.Dispose();
    _cancellationTokenSource.Dispose();
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
            foreach (Task t in _tasks.GetConsumingEnumerable(_cancellationTokenSource.Token))
            {
              if (_cancellationTokenSource.IsCancellationRequested)
              {
                break;
              }
              TryExecuteTask(t);
              if (_cancellationTokenSource.IsCancellationRequested)
              {
                break;
              }
            }
          }
#pragma warning disable CA1031
          catch (Exception)
#pragma warning restore CA1031
          {
            // ignored
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
