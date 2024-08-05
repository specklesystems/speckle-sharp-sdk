using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.Archicad;

/*
For further informations about given the variables, visit:
https://archicadapi.graphisoft.com/documentation/api_zonetype
*/
public class ArchicadRoom : Room
{
  // Element base
  public string elementType { get; set; }

  public List<Classification> classifications { get; set; }
  public Base? elementProperties { get; set; }
  public Base? componentProperties { get; set; }

  public override Level? level
  {
    get => archicadLevel;
    set => archicadLevel = value as ArchicadLevel ?? null;
  }

  [JsonIgnore]
  public ArchicadLevel? archicadLevel { get; set; }

  public string? layer { get; set; } /*APINullabe*/

  public ElementShape shape { get; set; }
}
