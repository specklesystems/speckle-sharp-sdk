using System.Text;

namespace Speckle.Sdk.Serialisation.V2.Send;

public readonly record struct BaseItem(Id Id, Json Json, bool NeedsStorage, Dictionary<Id, int>? Closures) : IHasSize
{
  public int Size { get; } = Encoding.UTF8.GetByteCount(Json.Value);

  public bool Equals(BaseItem? other)
  {
    if (other is null)
    {
      return false;
    }
    return string.Equals(Id.Value, other.Value.Id.Value, StringComparison.OrdinalIgnoreCase);
  }

  public override int GetHashCode() => Id.GetHashCode();
}
