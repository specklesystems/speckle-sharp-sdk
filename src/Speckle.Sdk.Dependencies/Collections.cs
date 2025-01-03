using System.Collections;
using System.Collections.Frozen;
using System.Diagnostics;

namespace Speckle.Sdk.Dependencies;

public static class Collections
{
  public static IReadOnlyCollection<T> Freeze<T>(this IEnumerable<T> source) => source.ToFrozenSet();

  public static IReadOnlyDictionary<TKey, TValue> Freeze<TKey, TValue>(this IDictionary<TKey, TValue> source)
    where TKey : notnull => source.ToFrozenDictionary();
}

public static class EnumerableExtensions
{
  public static IEnumerable<int> RangeFrom(int from, int to)
  {
    if (from - to == 0)
    {
      return Enumerable.Empty<int>();
    }

    return new RangeIterator(from, to);
  }

  /// <summary>
  /// An iterator that yields a range of consecutive integers.
  /// </summary>
  [DebuggerDisplay("Count = {CountForDebugger}")]
  private sealed partial class RangeIterator : IEnumerable<int>, IEnumerator<int>
  {
    private readonly int _start;
    private readonly int _end;
    private int _state;

    public RangeIterator(int start, int end)
    {
      _start = start;
      _end = end;
    }

    private int CountForDebugger => _end - _start;


    public  bool MoveNext()
    {
      switch (_state)
      {
        case 1:
          Debug.Assert(_start != _end);
          Current = _start;
          _state = 2;
          return true;
        case 2:
          if (unchecked(++Current) == _end)
          {
            break;
          }

          return true;
      }

      _state = -1;
      return false;
    }

    public void Reset() => throw new NotImplementedException();

    public int Current { get; private set; }

    object? IEnumerator.Current => Current;

    public  void Dispose() => _state = -1; // Don't reset current

    public IEnumerator<int> GetEnumerator() => this;

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
  }
}


