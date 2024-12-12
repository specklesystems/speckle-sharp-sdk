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
) : BatchingChannelReader<T, List<T>>(source, batchSize, singleReader, syncCont)
  where T : IHasSize
{
  private readonly int _batchSize = batchSize;

  protected override List<T> CreateBatch(int capacity) => new();

  protected override void TrimBatch(List<T> batch) => batch.TrimExcess();

  protected override void AddBatchItem(List<T> batch, T item) => batch.Add(item);

  protected override int GetBatchSize(List<T> batch)
  {
    int size = 0;
    foreach (T item in batch)
    {
      size += item.Size;
    }

    if (size >= _batchSize)
    {
      return _batchSize;
    }
    return size;
  }
}
