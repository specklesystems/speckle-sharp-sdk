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
) : BatchingChannelReader<T, Batch<T>>(x => new(x), source, batchSize, singleReader, syncCont)
  where T : IHasSize
{
  protected override Batch<T> CreateBatch(int capacity) => new(capacity);

  protected override void TrimBatch(ref Batch<T> batch, bool isVerifiedFull)
  {
    if (!isVerifiedFull)
    {
      batch.TrimExcess();
    }
  }

  protected override void AddBatchItem(Batch<T> batch, T item) => batch.Add(item);

  protected override int GetBatchSize(Batch<T> batch) => batch.Size;
}
