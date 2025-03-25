using System.Collections.Frozen;

namespace Speckle.Sdk.Dependencies;

public static class Collections
{
#if NET5_0_OR_GREATER
  public static IReadOnlySet<T> Freeze<T>(this IEnumerable<T> source)
#else
  public static IReadOnlyCollection<T> Freeze<T>(this IEnumerable<T> source)
#endif
  {
    return source.ToFrozenSet();
  }

  public static IReadOnlyDictionary<TKey, TValue> Freeze<TKey, TValue>(
    this IEnumerable<KeyValuePair<TKey, TValue>> source
  )
    where TKey : notnull => source.ToFrozenDictionary();
}

public static class EnumerableExtensions
{
  public static IEnumerable<int> RangeFrom(int from, int to) => Enumerable.Range(from, to - from + 1);

#if NETSTANDARD2_0
  public static IEnumerable<TSource> DistinctBy<TSource, TKey>(
    this IEnumerable<TSource> source,
    Func<TSource, TKey> keySelector
  )
  {
    var keys = new HashSet<TKey>();
    foreach (var element in source)
    {
      if (keys.Contains(keySelector(element)))
      {
        continue;
      }
      keys.Add(keySelector(element));
      yield return element;
    }
  }
#endif
}
