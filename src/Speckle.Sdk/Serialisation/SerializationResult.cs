namespace Speckle.Sdk.Serialisation;

[Obsolete(Json.DEPRECATION_MESSAGE)]
public readonly record struct SerializationResult(Json Json, Id? Id);

public readonly record struct Json
{
  public const string DEPRECATION_MESSAGE =
    "Since 2026.9 - JSON based send/receive pipelines are deprecated and replaced parquet bundle pipeline "
    + "See Operations.Send3, Operations.Receive3 for replacements"
    + ""
    + "see https://docs.speckle.systems/next/welcome";

  [Obsolete(DEPRECATION_MESSAGE)]
  public Json(string json)
  {
    Value = json ?? throw new ArgumentNullException(nameof(json));
  }

  public override string ToString() => Value;

  public string Value { get; }
}

[Obsolete(Json.DEPRECATION_MESSAGE)]
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
