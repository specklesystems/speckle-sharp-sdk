using System.Threading.Channels;
using Open.ChannelExtensions;

namespace Speckle.Sdk.Serialisation.V2.Send;

public interface IHasSize
{
  int Size { get; }
}

public class Batch<T>(int capacity) : IHasSize
  where T : IHasSize
{
#pragma warning disable IDE0032
  private readonly List<T> _items = new(capacity);
  private int _batchSize;
#pragma warning restore IDE0032

  public void Add(T item)
  {
    _items.Add(item);
    _batchSize += item.Size;
  }

  public void TrimExcess()
  {
    _items.TrimExcess();
    _batchSize = _items.Sum(x => x.Size);
  }

  public int Size => _batchSize;
  public List<T> Items => _items;
}

public class SizeBatchingChannelReader<T>(
  ChannelReader<T> source,
  int batchSize,
  bool singleReader,
  bool syncCont = false
) : BatchingChannelReader<T, Batch<T>>(source, batchSize, singleReader, syncCont)
  where T : IHasSize
{
  protected override Batch<T> CreateBatch(int capacity) => new(capacity);

  protected override void TrimBatch(Batch<T> batch) => batch.TrimExcess();

  protected override void AddBatchItem(Batch<T> batch, T item) => batch.Add(item);

  protected override int GetBatchSize(Batch<T> batch) => batch.Size;
}
