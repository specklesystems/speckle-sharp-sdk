using System.Buffers;
using Speckle.Sdk.Dependencies;

namespace Speckle.Sdk.Serialisation.V2.Send;

public sealed class Batch<T> : IHasSize, IMemoryOwner<T>
  where T : IHasSize
{
  private static readonly Pool<List<T>> _pool = Pools.CreateListPool<T>();
#pragma warning disable IDE0032
  private readonly List<T> _items = _pool.Get();
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
  public void Dispose() => _pool.Return(_items);

  public Memory<T> Memory => new Memory<T>(_items.ToArray());
}
