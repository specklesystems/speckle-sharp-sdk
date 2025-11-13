using System.Threading.Channels;

namespace Speckle.Sdk.Dependencies;

internal sealed class BroadcastChannel<T>
{
  private readonly List<Channel<T>> _subscribers = [];

  public ChannelReader<T> Subscribe()
  {
    var channel = Channel.CreateUnbounded<T>(new UnboundedChannelOptions() { SingleReader = true });
    _subscribers.Add(channel);
    return channel.Reader;
  }

  public async Task WriteAsync(T item, CancellationToken cancellationToken)
  {
    foreach (var sub in _subscribers)
    {
      await sub.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
    }
  }

  public bool IsReadingCompleted()
  {
    return _subscribers.All(x => x.Reader.Completion.IsCompleted);
  }

  public void CompleteWriters()
  {
    foreach (var sub in _subscribers)
    {
      sub.Writer.Complete();
    }
  }

  public async Task CompleteReaders()
  {
    await Task.WhenAll(_subscribers.Select(x => x.Reader.Completion)).ConfigureAwait(false);
  }
}
