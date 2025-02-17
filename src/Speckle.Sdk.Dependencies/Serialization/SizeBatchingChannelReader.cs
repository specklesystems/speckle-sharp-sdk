using System.Buffers;
using System.Threading.Channels;
using Open.ChannelExtensions;

namespace Speckle.Sdk.Serialisation.V2.Send;

public interface IHasSize
{
  int Size { get; }
}

public sealed class SizeBatchingChannelReader<T>(
  ChannelReader<T> source,
  int batchSize,
  bool singleReader,
  bool syncCont = false)
  : BatchingChannelReader<T, IMemoryOwner<T>>(x => new Batch<T>(), source, batchSize, singleReader, syncCont)
  where T : IHasSize
{
private readonly int _batchSize = batchSize;

protected override IMemoryOwner<T> CreateBatch(int capacity) => new Batch<T>();

  protected override void TrimBatch(ref IMemoryOwner<T> batch, bool isVerifiedFull)
  => BatchExtensions.TrimBatch(ref batch, isVerifiedFull);

  protected override void AddBatchItem(IMemoryOwner<T> batch, T item) => batch.AddBatchItem( item);

  protected override int GetBatchSize(IMemoryOwner<T> batch)=> batch.GetBatchSize( _batchSize);
}
