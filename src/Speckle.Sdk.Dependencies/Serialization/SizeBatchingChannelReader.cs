using System.Buffers;
using System.Threading.Channels;
using Open.ChannelExtensions;

namespace Speckle.Sdk.Serialisation.V2.Send;

public interface IHasByteSize
{
  int ByteSize { get; }
}

public sealed class SizeBatchingChannelReader<T>(
  ChannelReader<T> source,
  Action<string> logAsWarning,
  int batchSize,
  bool singleReader,
  bool syncCont = false
)
  : BatchingChannelReader<T, IMemoryOwner<T>>(
    _ => BatchExtensions.CreateBatch<T>(),
    source,
    batchSize,
    singleReader,
    syncCont
  )
  where T : IHasByteSize
{
  private readonly int _batchSize = batchSize;

  protected override IMemoryOwner<T> CreateBatch(int capacity) => BatchExtensions.CreateBatch<T>();

  protected override void TrimBatch(ref IMemoryOwner<T> batch, bool isVerifiedFull) =>
    BatchExtensions.TrimBatch(ref batch, isVerifiedFull);

  protected override void AddBatchItem(IMemoryOwner<T> batch, T item) => batch.AddBatchItem(item);

  protected override int GetBatchSize(IMemoryOwner<T> batch) => batch.GetBatchSize(logAsWarning, _batchSize);
}
