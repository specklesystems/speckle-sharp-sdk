using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Speckle.Sdk.Serialisation.V2.Send;

public class EmptyDictionary<TKey, TValue> : IDictionary<TKey, TValue>
{
  public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => throw new NotImplementedException();

  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

  public void Add(KeyValuePair<TKey, TValue> item) { }

  public void Clear() => throw new NotImplementedException();

  public bool Contains(KeyValuePair<TKey, TValue> item) => false;

  public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => throw new NotImplementedException();

  public bool Remove(KeyValuePair<TKey, TValue> item) => false;

  public int Count => 0;
  public bool IsReadOnly => false;

  public void Add(TKey key, TValue value) { }

  public bool ContainsKey(TKey key) => false;

  public bool Remove(TKey key) => false;

  public bool TryGetValue(TKey key, [UnscopedRef] out TValue value)
  {
    value = default!;
    return false;
  }

  public TValue this[TKey key]
  {
#pragma warning disable CA1065
    get => throw new NotImplementedException();
#pragma warning restore CA1065
    set { }
  }

  public ICollection<TKey> Keys { get; }
  public ICollection<TValue> Values { get; }
}
