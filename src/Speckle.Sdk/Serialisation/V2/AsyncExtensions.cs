using System.Diagnostics;

namespace Speckle.Sdk.Serialisation.V2;

public static class AsyncExtensions
{
  public static async IAsyncEnumerable<TItem> SelectManyAsync<TItem>(this IEnumerable<IAsyncEnumerable<TItem>> source)
  {
    // get enumerators from all inner IAsyncEnumerable
    var enumerators = source.Select(x => x.GetAsyncEnumerator()).ToList();

    List<Task<(IAsyncEnumerator<TItem>, bool)>> runningTasks = new();

    // start all inner IAsyncEnumerable
    foreach (var asyncEnumerator in enumerators)
    {
      runningTasks.Add(MoveNextWrapped(asyncEnumerator));
    }

    // while there are any running tasks
    while (runningTasks.Count != 0)
    {
      // get next finished task and remove it from list
      var finishedTask = await Task.WhenAny(runningTasks).ConfigureAwait(false);
      runningTasks.Remove(finishedTask);

      // get result from finished IAsyncEnumerable
      var result = await finishedTask.ConfigureAwait(false);
      var asyncEnumerator = result.Item1;
      var hasItem = result.Item2;

      // if IAsyncEnumerable has item, return it and put it back as running for next item
      if (hasItem)
      {
        yield return asyncEnumerator.Current;

        runningTasks.Add(MoveNextWrapped(asyncEnumerator));
      }
    }

    // don't forget to dispose, should be in finally
    foreach (var asyncEnumerator in enumerators)
    {
      await asyncEnumerator.DisposeAsync().ConfigureAwait(false);
    }
  }

  /// <summary>
  /// Helper method that returns Task with tuple of IAsyncEnumerable and it's result of MoveNextAsync.
  /// </summary>
  private static async Task<(IAsyncEnumerator<TItem>, bool)> MoveNextWrapped<TItem>(
    IAsyncEnumerator<TItem> asyncEnumerator
  )
  {
    var res = await asyncEnumerator.MoveNextAsync().ConfigureAwait(false);
    return (asyncEnumerator, res);
  }

  public static IAsyncEnumerable<TSource[]> BatchAsync<TSource>(this IAsyncEnumerable<TSource> source, int size) =>
    AsyncEnumerableChunkIterator(source, size);

  private static async IAsyncEnumerable<TSource[]> AsyncEnumerableChunkIterator<TSource>(
    IAsyncEnumerable<TSource> source,
    int size
  )
  {
#pragma warning disable CA2007
    await using IAsyncEnumerator<TSource> e = source.GetAsyncEnumerator();
#pragma warning restore CA2007

    // Before allocating anything, make sure there's at least one element.
    if (await e.MoveNextAsync().ConfigureAwait(false))
    {
      // Now that we know we have at least one item, allocate an initial storage array. This is not
      // the array we'll yield.  It starts out small in order to avoid significantly overallocating
      // when the source has many fewer elements than the chunk size.
      int arraySize = Math.Min(size, 4);
      int i;
      do
      {
        var array = new TSource[arraySize];

        // Store the first item.
        array[0] = e.Current;
        i = 1;

        if (size != array.Length)
        {
          // This is the first chunk. As we fill the array, grow it as needed.
          for (; i < size && await e.MoveNextAsync().ConfigureAwait(false); i++)
          {
            if (i >= array.Length)
            {
              arraySize = (int)Math.Min((uint)size, 2 * (uint)array.Length);
              Array.Resize(ref array, arraySize);
            }

            array[i] = e.Current;
          }
        }
        else
        {
          // For all but the first chunk, the array will already be correctly sized.
          // We can just store into it until either it's full or MoveNext returns false.
          TSource[] local = array; // avoid bounds checks by using cached local (`array` is lifted to iterator object as a field)
          Debug.Assert(local.Length == size);
          for (; (uint)i < (uint)local.Length && await e.MoveNextAsync().ConfigureAwait(false); i++)
          {
            local[i] = e.Current;
          }
        }

        if (i != array.Length)
        {
          Array.Resize(ref array, i);
        }

        yield return array;
      } while (i >= size && await e.MoveNextAsync().ConfigureAwait(false));
    }
  }

  public static IEnumerable<TSource[]> Batch<TSource>(this IEnumerable<TSource> source, int size)
  {
    if (source is TSource[] array)
    {
      // Special-case arrays, which have an immutable length. This enables us to not only do an
      // empty check and avoid allocating an iterator object when empty, it enables us to have a
      // much more efficient (and simpler) implementation for chunking up the array.
      return array.Length != 0 ? ArrayChunkIterator(array, size) : [];
    }

    return EnumerableChunkIterator(source, size);
  }

  private static IEnumerable<TSource[]> ArrayChunkIterator<TSource>(TSource[] source, int size)
  {
    int index = 0;
    while (index < source.Length)
    {
      TSource[] chunk = new ReadOnlySpan<TSource>(source, index, Math.Min(size, source.Length - index)).ToArray();
      index += chunk.Length;
      yield return chunk;
    }
  }

  private static IEnumerable<TSource[]> EnumerableChunkIterator<TSource>(IEnumerable<TSource> source, int size)
  {
    using IEnumerator<TSource> e = source.GetEnumerator();

    // Before allocating anything, make sure there's at least one element.
    if (e.MoveNext())
    {
      // Now that we know we have at least one item, allocate an initial storage array. This is not
      // the array we'll yield.  It starts out small in order to avoid significantly overallocating
      // when the source has many fewer elements than the chunk size.
      int arraySize = Math.Min(size, 4);
      int i;
      do
      {
        var array = new TSource[arraySize];

        // Store the first item.
        array[0] = e.Current;
        i = 1;

        if (size != array.Length)
        {
          // This is the first chunk. As we fill the array, grow it as needed.
          for (; i < size && e.MoveNext(); i++)
          {
            if (i >= array.Length)
            {
              arraySize = (int)Math.Min((uint)size, 2 * (uint)array.Length);
              Array.Resize(ref array, arraySize);
            }

            array[i] = e.Current;
          }
        }
        else
        {
          // For all but the first chunk, the array will already be correctly sized.
          // We can just store into it until either it's full or MoveNext returns false.
          TSource[] local = array; // avoid bounds checks by using cached local (`array` is lifted to iterator object as a field)
          Debug.Assert(local.Length == size);
          for (; (uint)i < (uint)local.Length && e.MoveNext(); i++)
          {
            local[i] = e.Current;
          }
        }

        if (i != array.Length)
        {
          Array.Resize(ref array, i);
        }

        yield return array;
      } while (i >= size && e.MoveNext());
    }
  }
}
