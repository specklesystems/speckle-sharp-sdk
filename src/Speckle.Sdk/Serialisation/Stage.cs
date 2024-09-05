using System.Threading.Channels;

namespace Speckle.Sdk.Serialisation;

public abstract class Stage<TConsumes, TProduces>
{
  private readonly Channel<TConsumes> _channel;

  protected Stage(Channel<TConsumes> channel)
  {
    _channel = channel;
  }
  
  public long Count => _channel.Reader.Count;
  
  protected ChannelReader<TConsumes> Reader => _channel.Reader;
  
  public async ValueTask WriteToStage(TConsumes message)
  {
    while (await _channel.Writer.WaitToWriteAsync().ConfigureAwait(false))
    {
      if (_channel.Writer.TryWrite(message))
      {
        return;
      }

      await Task.Delay(TimeSpan.FromMilliseconds(10)).ConfigureAwait(false);
    }
  }
  
  public Func<TProduces, ValueTask>? Produce { get; set; } 

  protected async ValueTask Run()
  {
    while (await Reader.WaitToReadAsync().ConfigureAwait(false))
    {
      if (Reader.TryRead(out TConsumes? message))
      {
        TProduces? produces = await Execute(message).ConfigureAwait(false);
        if (produces is not null && Produce is not null)
        {
          await Produce(produces).ConfigureAwait(false);
        }
      }
    }
  }

  protected abstract ValueTask<TProduces?> Execute(TConsumes message);
}
