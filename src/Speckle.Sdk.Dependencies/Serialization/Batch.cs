using System.Buffers;
using Speckle.Sdk.Dependencies;

namespace Speckle.Sdk.Serialisation.V2.Send;

public sealed class Batch<T> : IMemoryOwner<T>
  where T : IHasByteSize
{
  private static readonly Pool<List<T>> _pool = Pools.CreateListPool<T>();

  public List<T> Items { get; } = _pool.Get();
  public int BatchByteSize { get; private set; }

  public void Add(T item)
  {
    Items.Add(item);
    BatchByteSize += item.ByteSize;
  }

  public void TrimExcess()
  {
    Items.TrimExcess();
    BatchByteSize = Items.Sum(x => x.ByteSize);
  }

  public void Dispose() => _pool.Return(Items);

  public Memory<T> Memory => new(Items.ToArray());
}
