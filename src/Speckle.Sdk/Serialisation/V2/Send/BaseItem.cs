using System.Text;

namespace Speckle.Sdk.Serialisation.V2.Send;

public sealed record BaseItem(Id Id, Json Json, bool NeedsStorage, Dictionary<Id, int>? Closures) : IHasByteSize
{
  public int ByteSize { get; } = Encoding.UTF8.GetByteCount(Json.Value);

  public bool Equals(BaseItem? other)
  {
    if (other is null)
    {
      return false;
    }
    return string.Equals(Id.Value, other.Id.Value, StringComparison.OrdinalIgnoreCase);
  }

  public override int GetHashCode() => Id.GetHashCode();
}
