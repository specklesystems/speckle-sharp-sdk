using System.Buffers;
using Microsoft.Extensions.ObjectPool;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Common;

namespace Speckle.Sdk.Serialisation;

public class SpeckleObjectSerializerPool
{
  public static readonly SpeckleObjectSerializerPool Instance = new();

  private SpeckleObjectSerializerPool() { }

  public JsonTextWriter GetJsonTextWriter(TextWriter writer) => new(writer) { ArrayPool = _charPool };

  public JsonTextReader GetJsonTextReader(TextReader reader) => new(reader) { ArrayPool = _charPool };

  private readonly SerializerPool<char> _charPool = new(ArrayPool<char>.Create()); //use default values

  private class SerializerPool<T>(ArrayPool<T> pool) : IArrayPool<T>
  {
    public T[] Rent(int minimumLength) => pool.Rent(minimumLength);

    public void Return(T[]? array) => pool.Return(array.NotNull());
  }

  public ObjectPool<Dictionary<string, object?>> ObjectDictionaries { get; } =
    ObjectPool.Create(new ObjectDictionaryPolicy());

  private class ObjectDictionaryPolicy : IPooledObjectPolicy<Dictionary<string, object?>>
  {
    public Dictionary<string, object?> Create() => new(50, StringComparer.OrdinalIgnoreCase);

    public bool Return(Dictionary<string, object?> obj)
    {
      obj.Clear();
      return true;
    }
  }

  public ObjectPool<List<string>> ListString { get; } = ObjectPool.Create(new ListStringPolicy());

  private class ListStringPolicy : IPooledObjectPolicy<List<string>>
  {
    public List<string> Create() => new(20);

    public bool Return(List<string> obj)
    {
      obj.Clear();
      return true;
    }
  }
}
