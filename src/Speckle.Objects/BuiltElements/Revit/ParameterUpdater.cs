using Speckle.Objects.Utils;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.Revit;

[SpeckleType("Objects.BuiltElements.Revit.ParameterUpdater")]
public class ParameterUpdater : Base
{
  [SchemaInfo("ParameterUpdater", "Updates parameters on a Revit element by id", "Revit", "Families")]
  public ParameterUpdater([SchemaParamInfo("A Revit ElementId or UniqueId")] string id, List<Parameter> parameters)
  {
    elementId = id;
    this.parameters = parameters.ToBase();
  }

  public ParameterUpdater() { }

  public string elementId { get; set; }
  public Base? parameters { get; set; }
}
