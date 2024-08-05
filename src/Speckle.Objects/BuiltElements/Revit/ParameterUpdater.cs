using Speckle.Core.Kits;
using Speckle.Core.Models;
using Speckle.Objects.Utils;

namespace Speckle.Objects.BuiltElements.Revit;

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
