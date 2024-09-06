using System.Threading.Channels;
using Open.ChannelExtensions;

namespace Speckle.Sdk.Serialisation;

public interface IStageProcess
{
   long Queued { get; }
   long Dequeued  { get; }
   long Done  { get; }
}

public abstract class Stage<TConsumes, TProduces> : IStageProcess
{
#pragma warning disable IDE0032
  private long _dequeued;
  private long _done;
#pragma warning restore IDE0032
  
  private readonly int _batchSize;

  protected Stage(Channel<TConsumes> channel, int batchSize = 1)
  {
    Channel = channel;
    _batchSize = batchSize;
  }
  
  public long Queued => Channel.Reader.Count;
  public long Dequeued => _dequeued;
  public long Done => _done;
  
  protected ChannelReader<TConsumes> Reader => Channel.Reader;
  public ChannelWriter<TConsumes> Writer =>  Channel.Writer;
  public Channel<TConsumes> Channel {get;}

  public async ValueTask Run(Func<IReadOnlyList<TProduces>, ValueTask> channelWriter)
  {
    var batch = new List<TConsumes>();
    while (!Reader.Completion.IsCompleted)
    {
      batch.Clear();
      if (_batchSize <= 1)
      {
        batch.Add(await Reader.ReadAsync().ConfigureAwait(false));
      }
      else
      {
        batch.AddRange(await Reader.Batch(_batchSize).WithTimeout(TimeSpan.FromMilliseconds(500)).ReadAsync().ConfigureAwait(false));
      }
      if (batch.Count != 0)
      {
          _dequeued+=batch.Count;
          //Console.WriteLine($"{GetType()} Transforming message");
          var produces = await Execute(batch).ConfigureAwait(false);
          _done+=batch.Count;
          if (produces is not null)
          {
            //Console.WriteLine($"{GetType()} Producing message");
              await channelWriter(produces).ConfigureAwait(false);
          }
      }
    }
    Console.WriteLine(GetType() + "Done");
  }

  protected abstract ValueTask<IReadOnlyList<TProduces>> Execute(IReadOnlyList<TConsumes> message);
}
