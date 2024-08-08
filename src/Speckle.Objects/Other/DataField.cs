using Speckle.Sdk.Models;

namespace Speckle.Objects.Other;

/// <summary>
/// Generic class for a data field
/// </summary>
[SpeckleType("Objects.Other.DataField")]
public class DataField : Base
{
  public DataField() { }

  public DataField(string name, string type, object? value, string? units = null)
  {
    this.name = name;
    this.type = type;
    this.value = value;
    this.units = units;
  }

  public string name { get; set; }

  public string type { get; set; }

  public object? value { get; set; }

  public string? units { get; set; }
}
