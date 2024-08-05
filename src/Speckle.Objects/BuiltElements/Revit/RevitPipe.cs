using Speckle.Objects.BuiltElements.Revit.Interfaces;
using Speckle.Objects.Geometry;
using Speckle.Objects.Utils;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.Revit;

public class RevitPipe : Pipe, IHasMEPConnectors
{
  public RevitPipe() { }

  [SchemaInfo("RevitPipe", "Creates a Revit pipe", "Revit", "MEP")]
  public RevitPipe(
    string family,
    string type,
    [SchemaMainParam] ICurve baseCurve,
    double diameter,
    Level level,
    string systemName = "",
    string systemType = "",
    List<Parameter>? parameters = null
  )
  {
    this.family = family;
    this.type = type;
    this.baseCurve = baseCurve;
    this.diameter = diameter;
    this.systemName = systemName;
    this.systemType = systemType;
    this.level = level;
    this.parameters = parameters?.ToBase();
  }

  public string family { get; set; }
  public string type { get; set; }
  public string systemName { get; set; }
  public string systemType { get; set; }
  public Base? parameters { get; set; }
  public string elementId { get; set; }
  public Level level { get; set; }
  public List<RevitMEPConnector> Connectors { get; set; } = new();
}

public class RevitFlexPipe : RevitPipe
{
  public RevitFlexPipe() { }

  [SchemaInfo("RevitFlexPipe", "Creates a Revit flex pipe", "Revit", "MEP")]
  public RevitFlexPipe(
    string family,
    string type,
    [SchemaMainParam] ICurve baseCurve,
    double diameter,
    Level level,
    Vector startTangent,
    Vector endTangent,
    string systemName = "",
    string systemType = "",
    List<Parameter>? parameters = null
  )
  {
    this.family = family;
    this.type = type;
    this.baseCurve = baseCurve;
    this.diameter = diameter;
    this.startTangent = startTangent;
    this.endTangent = endTangent;
    this.systemName = systemName;
    this.systemType = systemType;
    this.level = level;
    this.parameters = parameters?.ToBase();
  }

  public Vector startTangent { get; set; }
  public Vector endTangent { get; set; }
}
