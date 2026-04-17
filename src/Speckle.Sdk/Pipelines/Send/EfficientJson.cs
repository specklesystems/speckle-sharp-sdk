using System.Buffers;
using System.Text;
using Speckle.Sdk.Dependencies;

namespace Speckle.Sdk.Pipelines.Send;

public sealed class EfficientJson : IDisposable
{
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

  private readonly ArrayBufferWriter<byte> _value = Pools.ArrayBufferWriter.Get();

  public void Dispose() => Pools.ArrayBufferWriter.Return(_value);
}
