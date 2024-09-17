using System.Buffers;
using Microsoft.IO;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Common;

namespace Speckle.Sdk.Serialisation.Send;

public class SpeckleObjectSerializer2Pool
{
  public static readonly SpeckleObjectSerializer2Pool Instance = new();

  private SpeckleObjectSerializer2Pool() { }

  public RecyclableMemoryStream GetMemoryStream() => _recyclableMemoryStreamManager.GetStream();

  public JsonTextWriter GetJsonTextWriter(Stream stream) => new(new StreamWriter(stream)) { ArrayPool = _charPool };

  public JsonTextWriter GetJsonTextWriter(TextWriter writer) => new(writer) { ArrayPool = _charPool };

  public JsonTextReader GetJsonTextReader(TextReader reader) => new(reader) { ArrayPool = _charPool };

  private readonly RecyclableMemoryStreamManager _recyclableMemoryStreamManager =
    new(
      new RecyclableMemoryStreamManager.Options()
      {
        LargeBufferMultiple = 1024 * 1024,
        MaximumBufferSize = 16 * 1024 * 1024,
        MaximumLargePoolFreeBytes = 16 * 1024 * 1024 * 4,
        MaximumSmallPoolFreeBytes = 40 * 1024 * 1024
      }
    );

  private readonly SerializerPool<char> _charPool = new(ArrayPool<char>.Create(4096, 4096));

  private class SerializerPool<T>(ArrayPool<T> pool) : IArrayPool<T>
  {
    public T[] Rent(int minimumLength) => pool.Rent(minimumLength);

    public void Return(T[]? array) => pool.Return(array.NotNull());
  }
}
