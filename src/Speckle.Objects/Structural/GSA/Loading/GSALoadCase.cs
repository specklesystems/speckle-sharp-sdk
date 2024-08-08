using Speckle.Objects.Structural.Loading;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Structural.GSA.Loading;

[SpeckleType("Objects.Structural.GSA.Loading.GSALoadCase")]
public class GSALoadCase : LoadCase
{
  public GSALoadCase() { }

  [SchemaInfo("GSALoadCase", "Creates a Speckle structural load case for GSA", "GSA", "Loading")]
  public GSALoadCase(
    int nativeId,
    string name,
    LoadType loadType,
    LoadDirection2D loadDirection,
    string? source = null,
    ActionType actionType = ActionType.None,
    string? description = null,
    string? include = null,
    bool bridge = false
  )
  {
    this.nativeId = nativeId;
    this.name = name;
    this.loadType = loadType;
    group = source ?? "";
    this.actionType = actionType;
    this.description = description ?? "";
    direction = loadDirection;
    this.include = include ?? "";
    this.bridge = bridge;
  }

  public int nativeId { get; set; }
  public LoadDirection2D direction { get; set; }
  public string include { get; set; }
  public bool bridge { get; set; }
}
