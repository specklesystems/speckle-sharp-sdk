using System.Buffers;
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
}
