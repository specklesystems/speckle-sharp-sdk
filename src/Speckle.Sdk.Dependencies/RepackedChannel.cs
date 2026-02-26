using System.Threading.Channels;

namespace Speckle.Sdk.Dependencies;

/// <summary>
/// For various reasons related to our use of ILRepack.FullAuto,
/// we cannot use Channels from the SDK project.
/// We have to keep usage of it inside the Sdk.Dependencies project.
///
/// For the sake of quick development, I've wrapped the <see cref="Channel"/> class here in a type
/// that is safe to use from the SDK project.
///
/// As and when we need more functions, we can add them here.
///
/// And yes... I'm not very happy about the way we've set this up
/// </summary>
/// <typeparam name="T"></typeparam>
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
}
