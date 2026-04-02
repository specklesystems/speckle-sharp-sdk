using System.Buffers;
using System.Text;
using Microsoft.Extensions.ObjectPool;

namespace Speckle.Sdk.Dependencies;

public sealed class ArrayBufferWriterPooledObjectPolicy<T> : PooledObjectPolicy<ArrayBufferWriter<T>>
{
  /// <summary>
  /// Gets or sets the initial capacity of pooled <see cref="ArrayBufferWriter{T}"/> instances.
  /// </summary>
  /// <value>Defaults to <c>100</c>.</value>
  public int InitialCapacity { get; set; } = 100;

  /// <summary>
  /// Gets or sets the maximum value for <see cref="StringBuilder.Capacity"/> that is allowed to be
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
