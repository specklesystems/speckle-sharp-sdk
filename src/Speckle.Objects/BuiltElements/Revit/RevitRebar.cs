using Speckle.Newtonsoft.Json;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.Revit;

[SpeckleType("Objects.BuiltElements.Revit.RevitRebarGroup")]
public class RevitRebarGroup : RebarGroup
{
  public RevitRebarGroup() { }

  [JsonIgnore]
  public RevitRebarShape revitShape { get; set; }

  public override RebarHook? startHook
  {
    get => revitStartHook;
    set =>
      revitStartHook = value switch
      {
        RevitRebarHook o => o,
        null => null,
        _ => throw new ArgumentException($"Expected object of type {nameof(RevitRebarHook)} or null"),
      };
  }

  [JsonIgnore]
  public RevitRebarHook? revitStartHook { get; set; }

  public override RebarHook? endHook
  {
    get => revitEndHook;
    set =>
      revitEndHook = value switch
      {
        RevitRebarHook o => o,
        null => null,
        _ => throw new ArgumentException($"Expected object of type {nameof(RevitRebarHook)} or null"),
      };
  }

  [JsonIgnore]
  public RevitRebarHook? revitEndHook { get; set; }

  public string family { get; set; }
  public string type { get; set; }
  public int barPositions { get; set; }
  public Vector normal { get; set; }
  public Base parameters { get; set; }
  public string elementId { get; set; }
}

[SpeckleType("Objects.BuiltElements.Revit.RevitRebarShape")]
public class RevitRebarShape : RebarShape
{
  public RevitRebarShape() { }

  public Base parameters { get; set; }
  public string elementId { get; set; }
}

[SpeckleType("Objects.BuiltElements.Revit.RevitRebarHook")]
public class RevitRebarHook : RebarHook
{
  public RevitRebarHook() { }

  public double multiplier { get; set; }
  public string orientation { get; set; }
  public Base parameters { get; set; }
  public string elementId { get; set; }
}

#region Obsolete
[Obsolete("Deprecated in 2.17: Use RevitRebarGroup class instead")]
[SpeckleType("Objects.BuiltElements.Revit.RevitRebar")]
public class RevitRebar : Rebar
{
  public string family { get; set; }
  public string type { get; set; }
  public string host { get; set; }
  public string barType { get; set; }
  public string barStyle { get; set; }
  public List<string> shapes { get; set; }
  public Base parameters { get; set; }
  public string elementId { get; set; }
}
#endregion
