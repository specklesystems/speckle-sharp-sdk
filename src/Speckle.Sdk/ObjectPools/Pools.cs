using System.Buffers;
using Microsoft.Extensions.ObjectPool;

namespace Speckle.Sdk.ObjectPools;

internal sealed class ObjectsPool<T>
  where T : class, new()
{
  private readonly ObjectPool<T> _objectPool;

  internal ObjectsPool(IPooledObjectPolicy<T> objectPolicy)
  {
    _objectPool = ObjectPool.Create(objectPolicy);
  }

  public T Get() => _objectPool.Get();

  public void Return(T obj) => _objectPool.Return(obj);
}

internal static class ObjectsPools
{
  internal static ObjectsPool<ArrayBufferWriter<byte>> ArrayBufferWriter { get; } =
    new(new ArrayBufferWriterPooledObjectPolicy<byte> { MaximumRetainedCapacity = 100 * 1024 * 1024 });
}

internal sealed class ArrayBufferWriterPooledObjectPolicy<T> : PooledObjectPolicy<ArrayBufferWriter<T>>
{
  /// <summary>
  /// Gets or sets the initial capacity of pooled <see cref="ArrayBufferWriter{T}"/> instances.
  /// </summary>
  /// <value>Defaults to <c>100</c>.</value>
  public int InitialCapacity { get; set; } = 100;

  /// <summary>
  /// Gets or sets the maximum value for <see cref="ArrayBufferWriter{T}.Capacity"/> that is allowed to be
  /// retained, when <see cref="Return(ArrayBufferWriter{T})"/> is invoked.
  /// </summary>
  /// <value>Defaults to <c>4096</c>.</value>
  public int MaximumRetainedCapacity { get; set; } = 4 * 1024;

  /// <inheritdoc />
  public override ArrayBufferWriter<T> Create()
  {
    return new ArrayBufferWriter<T>(InitialCapacity);
  }

  /// <inheritdoc />
  public override bool Return(ArrayBufferWriter<T> obj)
  {
    if (obj.Capacity > MaximumRetainedCapacity)
    {
      // Too big. Discard this one.
      return false;
    }

    obj.Clear();
    return true;
  }
}
