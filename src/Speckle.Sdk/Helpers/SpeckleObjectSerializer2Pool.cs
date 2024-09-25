using System.Buffers;
using Microsoft.Extensions.ObjectPool;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Common;

namespace Speckle.Sdk.Helpers;

public class SpeckleObjectSerializer2Pool
{
  public static readonly SpeckleObjectSerializer2Pool Instance = new();

  private SpeckleObjectSerializer2Pool() { }

  public JsonTextWriter GetJsonTextWriter(TextWriter writer) => new(writer) { ArrayPool = _charPool };

  public JsonTextReader GetJsonTextReader(TextReader reader) => new(reader) { ArrayPool = _charPool };

  private readonly SerializerPool<char> _charPool = new(ArrayPool<char>.Create(4096, 4096));

  private class SerializerPool<T>(ArrayPool<T> pool) : IArrayPool<T>
  {
    public T[] Rent(int minimumLength) => pool.Rent(minimumLength);

    public void Return(T[]? array) => pool.Return(array.NotNull());
  }

  public ObjectPool<Dictionary<string, object?>> DictPool { get; } =
    new DefaultObjectPoolProvider().Create(new DictPoolPolicy());
  
  private class DictPoolPolicy : PooledObjectPolicy<Dictionary<string, object?>>
  {
    public override Dictionary<string, object?> Create() => new(StringComparer.OrdinalIgnoreCase);

    public override bool Return(Dictionary<string, object?> obj)
    {
      obj.Clear();
      return true;
    }
  }
}
