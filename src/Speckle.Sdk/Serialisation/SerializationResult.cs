namespace Speckle.Sdk.Serialisation;

public readonly record struct SerializationResult(Json Json, Id? Id);

public readonly record struct Json(string Value)
{
  public override string ToString() => Value;
}

public readonly record struct Id(string Value)
{
  public override string ToString() => Value;

  public bool Equals(Id? other)
  {
    if (other is null)
    {
      return false;
    }

    return string.Equals(Value, other.Value.Value, StringComparison.OrdinalIgnoreCase);
  }
  public override int GetHashCode() => Value.GetHashCode();
}
