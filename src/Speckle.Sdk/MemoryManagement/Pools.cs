using System.Buffers;
using Microsoft.Extensions.ObjectPool;

namespace Speckle.Sdk.MemoryManagement;

internal static class Pools
{
  public static ObjectPool<ArrayBufferWriter<byte>> ArrayBufferWriter { get; } =
    ObjectPool.Create(
      new ArrayBufferWriterPooledObjectPolicy<byte>()
      {
        InitialCapacity = 512,
        MaximumRetainedCapacity = 100 * 1024 * 1024,
      }
    );
}
