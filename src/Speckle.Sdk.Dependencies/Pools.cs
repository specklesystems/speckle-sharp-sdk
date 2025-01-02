using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.ObjectPool;

namespace Speckle.Sdk.Dependencies;

public static class Pools
{
  public const int DefaultCapacity = 50;

  public static Pool<Dictionary<string, object?>> ObjectDictionaries { get; } = new(new ObjectDictionaryPolicy());

  private sealed class ObjectDictionaryPolicy : IPooledObjectPolicy<Dictionary<string, object?>>
  {
    public Dictionary<string, object?> Create() => new(50, StringComparer.OrdinalIgnoreCase);

    public bool Return(Dictionary<string, object?> obj)
    {
      obj.Clear();
      return true;
    }
  }

  public static Pool<StringBuilder> StringBuilders { get; } =
    new(new StringBuilderPooledObjectPolicy() { MaximumRetainedCapacity = 100 * 1024 * 1024 });

  private sealed class ObjectDictionaryPolicy<TKey, TValue> : IPooledObjectPolicy<Dictionary<TKey, TValue>>
    where TKey : notnull
  {
    public Dictionary<TKey, TValue> Create() => new(DefaultCapacity);

    public bool Return(Dictionary<TKey, TValue> obj)
    {
      obj.Clear();
      return true;
    }
  }

  private sealed class ObjectConcurrentDictionaryPolicy<TKey, TValue>
    : IPooledObjectPolicy<ConcurrentDictionary<TKey, TValue>>
    where TKey : notnull
  {
    public ConcurrentDictionary<TKey, TValue> Create() => new(Environment.ProcessorCount, DefaultCapacity);

    public bool Return(ConcurrentDictionary<TKey, TValue> obj)
    {
      obj.Clear();
      return true;
    }
  }

  private sealed class ObjectListPolicy<T> : IPooledObjectPolicy<List<T>>
  {
    public List<T> Create() => new(DefaultCapacity);

    public bool Return(List<T> obj)
    {
      obj.Clear();
      return true;
    }
  }

  public static Pool<List<T>> CreateListPool<T>() => new(new ObjectListPolicy<T>());

  public static Pool<Dictionary<TKey, TValue>> CreateDictionaryPool<TKey, TValue>()
    where TKey : notnull => new(new ObjectDictionaryPolicy<TKey, TValue>());

  public static Pool<ConcurrentDictionary<TKey, TValue>> CreateConcurrentDictionaryPool<TKey, TValue>()
    where TKey : notnull => new(new ObjectConcurrentDictionaryPolicy<TKey, TValue>());
}
