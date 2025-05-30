using Speckle.Automate.Sdk.Schema.Triggers;
using Speckle.Newtonsoft.Json;

namespace Speckle.Automate.Sdk.Schema;

///<summary>
/// Values of the project, model and automation that triggered this function run.
///</summary>
public readonly struct AutomationRunData
{
  [JsonRequired]
  public required string ProjectId { get; init; }

  [JsonRequired]
  public required Uri SpeckleServerUrl { get; init; }

  [JsonRequired]
  public required string AutomationId { get; init; }

  [JsonRequired]
  public required string AutomationRunId { get; init; }

  [JsonRequired]
  public required string FunctionRunId { get; init; }

  [JsonRequired]
  public required List<VersionCreationTrigger> Triggers { get; init; }
}
