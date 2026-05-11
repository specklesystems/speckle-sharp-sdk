using System.Buffers;
using System.Text;
using System.Text.Json;
using Speckle.Sdk.MemoryManagement;

namespace Speckle.Sdk.Pipelines.Send;

/// <summary>
/// Wrapper for a UTF8 Encoded array buffer
/// More efficient to write JSON via <see cref="Utf8JsonWriter"/> than as <see cref="string"/>s
/// </summary>
/// <remarks>
/// Mutability is exposed only internally via <see cref="Utf8Json.Writer"/> to <see cref="Serializer"/>
/// For all other consumers this class is read-only.
/// </remarks>
public sealed class Utf8Json : IDisposable
{
  private readonly ArrayBufferWriter<byte> _value;
  internal Utf8JsonWriter Writer { get; }

  public Utf8Json()
  {
    _value = Pools.ArrayBufferWriter.Get();

    Writer = new(_value);
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
