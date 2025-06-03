namespace Speckle.Sdk.Serialisation;

public readonly record struct SerializationResult(Json Json, Id? Id);

public readonly record struct Json
{
  public Json(string json)
  {
    Value = json ?? throw new ArgumentNullException(nameof(json));
  }

  public override string ToString() => Value;

  public string Value { get; }
}

public readonly record struct Id
{
  public Id(string id)
  {
    Value = id ?? throw new ArgumentNullException(nameof(id));
  }

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

  public string Value { get; }
}
