namespace Speckle.Sdk.Serialisation.V2.Send;

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
