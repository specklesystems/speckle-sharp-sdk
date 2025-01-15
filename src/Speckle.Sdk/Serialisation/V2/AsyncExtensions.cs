namespace Speckle.Sdk.Serialisation.V2;

public static class AsyncExtensions
{
  public static async ValueTask<TItem> FirstAsync<TItem>(this IAsyncEnumerable<TItem> source)
  {
    var e = source.GetAsyncEnumerator();
    if (await e.MoveNextAsync().ConfigureAwait(false))
    {
      return e.Current;
    }
    throw new InvalidOperationException("Sequence contains no elements");
  }
  
  #if NETSTANDARD2_0
  public static IEnumerable<T[]> Chunk<T>(this IEnumerable<T> source, int chunkSize)
  {
    List<T> list = new(chunkSize);
    foreach(T item in source)
    {
      list.Add(item);
      if(list.Count == chunkSize)
      {
        yield return list.ToArray();
        list = new List<T>(chunkSize);
      }
    }
    //don't forget the last one!
    if(list.Count != 0)
    {
      yield return list.ToArray();
    }
  }
#endif
}
