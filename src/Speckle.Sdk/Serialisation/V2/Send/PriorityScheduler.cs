using System.Collections.Concurrent;

namespace Speckle.Sdk.Serialisation.V2.Send;

public sealed class PriorityScheduler(ThreadPriority priority) : TaskScheduler, IDisposable
{
  private readonly BlockingCollection<Task> _tasks = new BlockingCollection<Task>();
  private Thread[]? _threads;
  private readonly int _maximumConcurrencyLevel = Math.Max(1, Environment.ProcessorCount);

  public void Dispose() => _tasks.Dispose();

  public override int MaximumConcurrencyLevel => _maximumConcurrencyLevel;

  protected override IEnumerable<Task> GetScheduledTasks() => _tasks;

  protected override void QueueTask(Task task)
  {
    _tasks.Add(task);

    if (_threads == null)
    {
      _threads = new Thread[_maximumConcurrencyLevel];
      for (int i = 0; i < _threads.Length; i++)
      {
        _threads[i] = new Thread(() =>
        {
          foreach (Task t in _tasks.GetConsumingEnumerable())
          {
            TryExecuteTask(t);
          }
        }) { Name = $"PriorityScheduler: {i}", Priority = priority, IsBackground = true };
        _threads[i].Start();
      }
    }
  }

  protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => false; // we might not want to execute task that should schedule as high or low priority inline
}
