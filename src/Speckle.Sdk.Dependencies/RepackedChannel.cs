using System.Threading.Channels;

namespace Speckle.Sdk.Dependencies;

public sealed class RepackedChannel<T>
{
  private readonly Channel<T> _channel;

  public RepackedChannel(int capacity, bool singleReader, bool singleWriter)
  {
    _channel = Channel.CreateBounded<T>(
      new BoundedChannelOptions(capacity)
      {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = singleReader,
        SingleWriter = singleWriter,
      }
    );
  }

  public void CompleteWriter() => _channel.Writer.Complete();

  public ValueTask WriteAsync(T item, CancellationToken cancellationToken) =>
    _channel.Writer.WriteAsync(item, cancellationToken);

  public IAsyncEnumerable<T> ReadAllAsync(CancellationToken cancellationToken) =>
    _channel.Reader.ReadAllAsync(cancellationToken);

  public async Task ReadAllAsync(Func<T, Task> callback, CancellationToken cancellationToken)
  {
    await foreach (var item in _channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
    {
      await callback(item).ConfigureAwait(false);
    }
  }
}
