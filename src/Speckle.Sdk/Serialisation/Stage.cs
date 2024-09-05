using System.Threading.Channels;

namespace Speckle.Sdk.Serialisation;

public interface IStageProcess
{
   long Queued { get; }
   long Dequeued  { get; }
   long Done  { get; }
}

public abstract class Stage<TConsumes, TProduces> : IStageProcess
{
  private readonly Channel<TConsumes> _channel;
#pragma warning disable IDE0032
  private long _dequeued;
  private long _done;
#pragma warning restore IDE0032

  protected Stage(Channel<TConsumes> channel)
  {
    _channel = channel;
  }
  
  public long Queued => _channel.Reader.Count;
  public long Dequeued => _dequeued;
  public long Done => _done;
  
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

  public async ValueTask Run()
  {
    while (await Reader.WaitToReadAsync().ConfigureAwait(false))
    {
      if (Reader.TryRead(out TConsumes? message))
      {
        _dequeued++;
        //Console.WriteLine($"{GetType()} Transforming message");
        TProduces? produces = await Execute(message).ConfigureAwait(false);
        _done++;
        if (produces is not null && Produce is not null)
        {
         // Console.WriteLine($"{GetType()} Producing message");
          await Produce(produces).ConfigureAwait(false);
        }
      }
    }
  }

  protected abstract ValueTask<TProduces?> Execute(TConsumes message);
}
