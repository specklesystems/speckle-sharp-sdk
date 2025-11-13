using System.Text;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Serialisation.V2.Send;

public record BaseItem(Id Id, Json Json, bool NeedsStorage, Dictionary<Id, int>? Closures) : IHasByteSize
{
  public virtual int ByteSize { get; } = Encoding.UTF8.GetByteCount(Json.Value);

  public virtual bool Equals(BaseItem? other)
  {
    if (other is null)
    {
      return false;
    }
    return string.Equals(Id.Value, other.Id.Value, StringComparison.OrdinalIgnoreCase);
  }

  public override int GetHashCode() => Id.GetHashCode();
}

public sealed record BlobItem(Id Id, Json Json, bool NeedsStorage, Dictionary<Id, int>? Closures, Blob Blob)
  : BaseItem(Id, Json, NeedsStorage, Closures)
{
  public Blob Blob { get; } = Blob;
  public override int ByteSize { get; } = (int)Blob.FileInfo.Length;
}
