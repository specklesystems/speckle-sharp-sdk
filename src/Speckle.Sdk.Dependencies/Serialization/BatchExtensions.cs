using System.Buffers;

namespace Speckle.Sdk.Serialisation.V2.Send;

public static class BatchExtensions
{
  public static Batch<T> CreateBatch<T>()
    where T : IHasByteSize => new();

  public static void TrimBatch<T>(ref IMemoryOwner<T> batch, bool isVerifiedFull)
    where T : IHasByteSize
  {
    if (!isVerifiedFull)
    {
      ((Batch<T>)batch).TrimExcess();
    }
  }

  public static void AddBatchItem<T>(this IMemoryOwner<T> batch, T item)
    where T : IHasByteSize => ((Batch<T>)batch).Add(item);

  public static int GetBatchSize<T>(this IMemoryOwner<T> batch, int maxBatchSize)
    where T : IHasByteSize
  {
    var currentSize = ((Batch<T>)batch).BatchByteSize;
    if (currentSize > maxBatchSize)
    {
      return maxBatchSize;
    }

    return currentSize;
  }
}
