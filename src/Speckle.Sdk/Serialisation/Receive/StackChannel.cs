using System.Collections.Concurrent;
using Speckle.Sdk.Common;

namespace Speckle.Sdk.Serialisation.Receive;

public sealed class StackChannel<T> : IDisposable
{
  private readonly ConcurrentStack<T> _stack = new();
  private readonly SemaphoreSlim _readerFinishedSemaphore = new(0);
  private readonly SemaphoreSlim _readSemaphore = new(0);

  private bool _completed;
  private Func<T, Task<bool>>? _action;

  public void Start(Func<T, Task<bool>> action)
  {
    _action = action;
    var task = new Task(Reader, TaskCreationOptions.LongRunning);
    task.Start();
  }
  private async void Reader()
  {
    _action.NotNull();
    while (true)
    {
      await _readSemaphore.WaitAsync().ConfigureAwait(false);
      while (_stack.TryPop(out var item))
      {
        if (await _action(item).ConfigureAwait(false))
        {
          _completed = true;
        }
      }

      if (_completed)
      {
        break;
      }
    }

    _readerFinishedSemaphore.Release();
  }
  public void Write(T item)
  {
    _stack.Push(item);
    _readSemaphore.Release();
  }
  public void Write(T[] items)
  {
      _stack.PushRange(items);
      _readSemaphore.Release();
  }

  public async Task CompleteAndWaitForReader()
  {
    _readSemaphore.Release();
    await _readerFinishedSemaphore.WaitAsync().ConfigureAwait(false);
  }

  public void Dispose()
  {
    _readerFinishedSemaphore.Dispose();
    _readSemaphore.Dispose();
  }
}
