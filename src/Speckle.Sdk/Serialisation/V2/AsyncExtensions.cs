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
}
