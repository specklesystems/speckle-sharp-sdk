using System.Buffers;

namespace Speckle.Sdk.Serialisation.V2.Send;

public static class BatchExtensions
{
  public static Batch<T> CreateBatch<T>()
    where T : IHasSize => new();

  public static void TrimBatch<T>(ref IMemoryOwner<T> batch, bool isVerifiedFull)
    where T : IHasSize
  {
    if (!isVerifiedFull)
    {
      ((Batch<T>)batch).TrimExcess();
    }
  }

  public static void AddBatchItem<T>(this IMemoryOwner<T> batch, T item)
    where T : IHasSize => ((Batch<T>)batch).Add(item);

  public static int GetBatchSize<T>(this IMemoryOwner<T> batch, int maxBatchSize)
    where T : IHasSize
  {
    var currentSize = ((Batch<T>)batch).Size;
    if (currentSize > maxBatchSize)
    {
      return maxBatchSize;
    }

    return currentSize;
  }
}
