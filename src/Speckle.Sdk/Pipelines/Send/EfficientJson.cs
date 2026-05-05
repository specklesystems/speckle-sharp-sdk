using System.Buffers;
using System.Text;
using System.Text.Json;
using Speckle.Sdk.MemoryManagement;

namespace Speckle.Sdk.Pipelines.Send;

public sealed class EfficientJson : IDisposable
{
  private readonly ArrayBufferWriter<byte> _value;
  internal Utf8JsonWriter Writer { get; }

  public EfficientJson()
  {
    _value = Pools.ArrayBufferWriter.Get();
    // try
    // {
    Writer = new(_value);
    // }
    // catch (MissingMemberException)
    // {
    // Test for
    // Console.WriteLine("Ctor failed, trying activator create instance");
    // Writer = (Utf8JsonWriter)typeof(Utf8JsonWriter).GetConstructors()[0].Invoke([_value, new JsonWriterOptions()]);
    // Activator.CreateInstance(typeof(Utf8JsonWriter), [_value, new JsonWriterOptions()]).NotNull();
    // }
  }

  internal IBufferWriter<byte> Buffer => _value;

  public ReadOnlySpan<byte> WrittenSpan => _value.WrittenSpan;

  public ReadOnlyMemory<byte> WrittenMemory => _value.WrittenMemory;

#if NET5_0_OR_GREATER
  public string ToJsonString() => Encoding.UTF8.GetString(WrittenSpan);
#else
  public byte[] GetInternalBuffer() => _value.InternalBuffer;

  public void CheckAndResizeBuffer(int sizeHint) => _value.CheckAndResizeBuffer(sizeHint);

  public string ToJsonString() => Encoding.UTF8.GetString(_value.InternalBuffer, 0, _value.WrittenCount);
#endif

  public int WrittenCount => _value.WrittenCount;

  public void Dispose() => Pools.ArrayBufferWriter.Return(_value);
}
