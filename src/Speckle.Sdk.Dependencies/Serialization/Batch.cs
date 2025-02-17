using System.Buffers;
using Speckle.Sdk.Dependencies;

namespace Speckle.Sdk.Serialisation.V2.Send;

public sealed class Batch<T> : IMemoryOwner<T>
  where T : IHasByteSize
{
  private static readonly Pool<List<T>> _pool = Pools.CreateListPool<T>();
#pragma warning disable IDE0032
  private readonly List<T> _items = _pool.Get();
  private int _batchByteSize;
#pragma warning restore IDE0032

  public void Add(T item)
  {
    _items.Add(item);
    _batchByteSize += item.ByteSize;
  }

  public void TrimExcess()
  {
    _items.TrimExcess();
    _batchByteSize = _items.Sum(x => x.ByteSize);
  }

  public int BatchByteSize => _batchByteSize;
  public List<T> Items => _items;

  public void Dispose() => _pool.Return(_items);

  public Memory<T> Memory => new(_items.ToArray());
}
