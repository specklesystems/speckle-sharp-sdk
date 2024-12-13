﻿using System.Text;
using Microsoft.Extensions.ObjectPool;

namespace Speckle.Sdk.Dependencies;

public static class Pools
{
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
    public Dictionary<TKey, TValue> Create() => new(50);

    public bool Return(Dictionary<TKey, TValue> obj)
    {
      obj.Clear();
      return true;
    }
  }

  private sealed class ObjectListPolicy<T> : IPooledObjectPolicy<List<T>>
  {
    public List<T> Create() => new(50);

    public bool Return(List<T> obj)
    {
      obj.Clear();
      return true;
    }
  }

  public static Pool<List<T>> CreateListPool<T>() => new(new ObjectListPolicy<T>());

  public static Pool<Dictionary<TKey, TValue>> CreateDictionaryPool<TKey, TValue>()
    where TKey : notnull => new(new ObjectDictionaryPolicy<TKey, TValue>());
}
