using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Speckle.Automate.Sdk.Schema.Triggers;

/// <summary>
/// Represents a single version creation trigger for the automation run.
/// </summary>
public sealed class VersionCreationTrigger : AutomationRunTriggerBase
{
  public const string VERSION_CREATION_TRIGGER_TYPE = "versionCreation";

  [JsonRequired]
  public required VersionCreationTriggerPayload Payload { get; init; }

  public VersionCreationTrigger() { }

  [SetsRequiredMembers]
  public VersionCreationTrigger(string modelId, string versionId)
  {
    Payload = new() { ModelId = modelId, VersionId = versionId };
    TriggerType = VERSION_CREATION_TRIGGER_TYPE;
  }
}

/// <summary>
/// Represents the version creation trigger payload.
/// </summary>
public sealed record VersionCreationTriggerPayload
{
  [JsonRequired]
  public required string ModelId { get; init; }

  [JsonRequired]
  public required string VersionId { get; init; }
}
