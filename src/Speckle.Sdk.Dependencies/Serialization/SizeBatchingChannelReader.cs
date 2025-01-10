using System.Buffers;
using System.Threading.Channels;
using Open.ChannelExtensions;

namespace Speckle.Sdk.Serialisation.V2.Send;

public interface IHasSize
{
  int Size { get; }
}

public class SizeBatchingChannelReader<T>(
  ChannelReader<T> source,
  int batchSize,
  bool singleReader,
  bool syncCont = false
) : BatchingChannelReader<T, IMemoryOwner<T>>(x => new Batch<T>(), source, batchSize, singleReader, syncCont)
  where T : IHasSize
{
  protected override IMemoryOwner<T> CreateBatch(int capacity) => new Batch<T>();

  protected override void TrimBatch(ref IMemoryOwner<T> batch, bool isVerifiedFull)
  {
    if (!isVerifiedFull)
    {
      ((Batch<T>)batch).TrimExcess();
    }
  }

  protected override void AddBatchItem(IMemoryOwner<T> batch, T item) => ((Batch<T>)batch).Add(item);

  protected override int GetBatchSize(IMemoryOwner<T> batch) => ((Batch<T>)batch).Size;
}
