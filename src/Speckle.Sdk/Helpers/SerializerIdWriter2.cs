using System.Text;
using Speckle.Newtonsoft.Json;

namespace Speckle.Sdk.Helpers;

public sealed class SerializerIdWriter2 : JsonWriter
{
  private readonly JsonWriter _jsonWriter;
#pragma warning disable CA2213
  private readonly JsonTextWriter _jsonIdWriter;
#pragma warning restore CA2213
  private readonly StreamWriter _idWriter;
  private readonly MemoryStream _memoryStream;
  
  protected override void Dispose(bool disposing)
  {
    base.Dispose(disposing);
    _idWriter.Dispose();
    _memoryStream.Dispose();
  }

  public SerializerIdWriter2(JsonWriter jsonWriter, SpeckleObjectSerializer2Pool pool)
  {
    _jsonWriter = jsonWriter;
    _memoryStream = pool.GetMemoryStream();
    _idWriter = new StreamWriter(_memoryStream);
    _jsonIdWriter = pool.GetJsonTextWriter(_idWriter);
  }

  public string FinishIdWriter()
  {
    _jsonIdWriter.WriteEndObject();
    _jsonIdWriter.Flush();
    var s = Encoding.UTF8.GetString(_memoryStream.GetBuffer(), 0, (int)_memoryStream.Length);
    ((IDisposable)_jsonIdWriter).Dispose();
    return s;
  }

  public override void WriteValue(string? value)
  {
    _jsonWriter.WriteValue(value);
    _jsonIdWriter.WriteValue(value);
  }

  public override void WriteStartArray()
  {
    _jsonWriter.WriteStartArray();
    _jsonIdWriter.WriteStartArray();
  }

  public override void WriteEndArray()
  {
    _jsonWriter.WriteEndArray();
    _jsonIdWriter.WriteEndArray();
  }

  public override void WriteStartObject()
  {
    _jsonWriter.WriteStartObject();
    _jsonIdWriter.WriteStartObject();
  }

  public override void WriteEndObject()
  {
    _jsonWriter.WriteEndObject();
    _jsonIdWriter.WriteEndObject();
  }

  public override void WriteComment(string? text)
  {
    _jsonWriter.WriteComment(text);
    _jsonIdWriter.WriteComment(text);
  }

  public override void WritePropertyName(string name)
  {
    _jsonWriter.WritePropertyName(name);
    _jsonIdWriter.WritePropertyName(name);
  }

  public override void WriteNull()
  {
    _jsonWriter.WriteNull();
    _jsonIdWriter.WriteNull();
  }

  public override void WriteUndefined()
  {
    _jsonWriter.WriteUndefined();
    _jsonIdWriter.WriteUndefined();
  }

  public override void WriteRaw(string? json)
  {
    _jsonWriter.WriteRaw(json);
    _jsonIdWriter.WriteRaw(json);
  }

  public override void WriteRawValue(string? json)
  {
    _jsonWriter.WriteRawValue(json);
    _jsonIdWriter.WriteRawValue(json);
  }

  public override void WriteValue(bool value)
  {
    _jsonWriter.WriteValue(value);
    _jsonIdWriter.WriteValue(value);
  }

  public override void WriteValue(bool? value)
  {
    _jsonWriter.WriteValue(value);
    _jsonIdWriter.WriteValue(value);
  }

  public override void WriteValue(byte value)
  {
    _jsonWriter.WriteValue(value);
    _jsonIdWriter.WriteValue(value);
  }

  public override void WriteValue(byte? value)
  {
    _jsonWriter.WriteValue(value);
    _jsonIdWriter.WriteValue(value);
  }

  public override void WriteValue(char value)
  {
    _jsonWriter.WriteValue(value);
    _jsonIdWriter.WriteValue(value);
  }

  public override void WriteValue(char? value)
  {
    _jsonWriter.WriteValue(value);
    _jsonIdWriter.WriteValue(value);
  }

  public override void WriteValue(DateTime value)
  {
    _jsonWriter.WriteValue(value);
    _jsonIdWriter.WriteValue(value);
  }

  public override void WriteValue(DateTime? value)
  {
    _jsonWriter.WriteValue(value);
    _jsonIdWriter.WriteValue(value);
  }

  public override void WriteValue(DateTimeOffset value)
  {
    _jsonWriter.WriteValue(value);
    _jsonIdWriter.WriteValue(value);
  }

  public override void WriteValue(DateTimeOffset? value)
  {
    _jsonWriter.WriteValue(value);
    _jsonIdWriter.WriteValue(value);
  }

  public override void WriteValue(decimal value)
  {
    _jsonWriter.WriteValue(value);
    _jsonIdWriter.WriteValue(value);
  }

  public override void WriteValue(decimal? value)
  {
    _jsonWriter.WriteValue(value);
    _jsonIdWriter.WriteValue(value);
  }

  public override void WriteValue(double value)
  {
    _jsonWriter.WriteValue(value);
    _jsonIdWriter.WriteValue(value);
  }

  public override void WriteValue(double? value)
  {
    _jsonWriter.WriteValue(value);
    _jsonIdWriter.WriteValue(value);
  }

  public override void WriteValue(float value)
  {
    _jsonWriter.WriteValue(value);
    _jsonIdWriter.WriteValue(value);
  }

  public override void WriteValue(float? value)
  {
    _jsonWriter.WriteValue(value);
    _jsonIdWriter.WriteValue(value);
  }

  public override void WriteValue(Guid value)
  {
    _jsonWriter.WriteValue(value);
    _jsonIdWriter.WriteValue(value);
  }

  public override void WriteValue(Guid? value)
  {
    _jsonWriter.WriteValue(value);
    _jsonIdWriter.WriteValue(value);
  }

  public override void WriteValue(int value)
  {
    _jsonWriter.WriteValue(value);
    _jsonIdWriter.WriteValue(value);
  }

  public override void WriteValue(int? value)
  {
    _jsonWriter.WriteValue(value);
    _jsonIdWriter.WriteValue(value);
  }

  public override void WriteValue(long value)
  {
    _jsonWriter.WriteValue(value);
    _jsonIdWriter.WriteValue(value);
  }

  public override void WriteValue(long? value)
  {
    _jsonWriter.WriteValue(value);
    _jsonIdWriter.WriteValue(value);
  }

  public override void WriteValue(sbyte value)
  {
    _jsonWriter.WriteValue(value);
    _jsonIdWriter.WriteValue(value);
  }

  public override void WriteValue(sbyte? value)
  {
    _jsonWriter.WriteValue(value);
    _jsonIdWriter.WriteValue(value);
  }

  public override void WriteValue(short value)
  {
    _jsonWriter.WriteValue(value);
    _jsonIdWriter.WriteValue(value);
  }

  public override void WriteValue(short? value)
  {
    _jsonWriter.WriteValue(value);
    _jsonIdWriter.WriteValue(value);
  }

  public override void WriteValue(TimeSpan value)
  {
    _jsonWriter.WriteValue(value);
    _jsonIdWriter.WriteValue(value);
  }

  public override void WriteValue(TimeSpan? value)
  {
    _jsonWriter.WriteValue(value);
    _jsonIdWriter.WriteValue(value);
  }

  public override void WriteValue(uint value)
  {
    _jsonWriter.WriteValue(value);
    _jsonIdWriter.WriteValue(value);
  }

  public override void WriteValue(uint? value)
  {
    _jsonWriter.WriteValue(value);
    _jsonIdWriter.WriteValue(value);
  }

  public override void WriteValue(ulong value)
  {
    _jsonWriter.WriteValue(value);
    _jsonIdWriter.WriteValue(value);
  }

  public override void WriteValue(ulong? value)
  {
    _jsonWriter.WriteValue(value);
    _jsonIdWriter.WriteValue(value);
  }

  public override void WriteValue(Uri? value)
  {
    _jsonWriter.WriteValue(value);
    _jsonIdWriter.WriteValue(value);
  }

  public override void WriteValue(ushort value)
  {
    _jsonWriter.WriteValue(value);
    _jsonIdWriter.WriteValue(value);
  }

  public override void WriteValue(ushort? value)
  {
    _jsonWriter.WriteValue(value);
    _jsonIdWriter.WriteValue(value);
  }

  public override void WriteValue(byte[]? value)
  {
    _jsonWriter.WriteValue(value);
    _jsonIdWriter.WriteValue(value);
  }

  public override void WriteValue(object? value)
  {
    _jsonWriter.WriteValue(value);
    _jsonIdWriter.WriteValue(value);
  }

  public override void Flush()
  {
    _jsonWriter.Flush();
    _jsonIdWriter.Flush();
  }

  public override async Task WriteValueAsync(string? value, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteStartArrayAsync(CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteStartArrayAsync(cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteStartArrayAsync(cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteEndArrayAsync(CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteEndArrayAsync(cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteEndArrayAsync(cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteStartObjectAsync(CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteStartObjectAsync(cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteStartObjectAsync(cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteEndObjectAsync(CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteEndObjectAsync(cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteEndObjectAsync(cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteCommentAsync(string? text, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteCommentAsync(text, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteCommentAsync(text, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WritePropertyNameAsync(string name, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WritePropertyNameAsync(name, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WritePropertyNameAsync(name, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteNullAsync(CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteNullAsync(cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteNullAsync(cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteUndefinedAsync(CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteUndefinedAsync(cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteUndefinedAsync(cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteRawAsync(string? json, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteRawAsync(json, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteRawAsync(json, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteRawValueAsync(string? json, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteRawValueAsync(json, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteRawValueAsync(json, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteValueAsync(bool value, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteValueAsync(bool? value, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteValueAsync(byte value, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteValueAsync(byte? value, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteValueAsync(char value, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteValueAsync(char? value, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteValueAsync(DateTime value, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteValueAsync(DateTime? value, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteValueAsync(DateTimeOffset value, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteValueAsync(DateTimeOffset? value, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteValueAsync(decimal value, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteValueAsync(decimal? value, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteValueAsync(double value, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteValueAsync(double? value, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteValueAsync(float value, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteValueAsync(float? value, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteValueAsync(Guid value, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteValueAsync(Guid? value, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteValueAsync(int value, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteValueAsync(int? value, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteValueAsync(long value, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteValueAsync(long? value, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteValueAsync(sbyte value, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteValueAsync(sbyte? value, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteValueAsync(short value, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteValueAsync(short? value, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteValueAsync(TimeSpan value, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteValueAsync(TimeSpan? value, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteValueAsync(uint value, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteValueAsync(uint? value, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteValueAsync(ulong value, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteValueAsync(ulong? value, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteValueAsync(Uri? value, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteValueAsync(ushort value, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteValueAsync(ushort? value, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteValueAsync(byte[]? value, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
  }

  public override async Task WriteValueAsync(object? value, CancellationToken cancellationToken = default)
  {
    await _jsonWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.WriteValueAsync(value, cancellationToken).ConfigureAwait(false);
  }

  public override async Task FlushAsync(CancellationToken cancellationToken = default)
  {
    await _jsonWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
    await _jsonIdWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
  }
}
