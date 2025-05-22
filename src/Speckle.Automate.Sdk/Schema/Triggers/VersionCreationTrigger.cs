using System.Diagnostics.CodeAnalysis;

namespace Speckle.Automate.Sdk.Schema.Triggers;

/// <summary>
/// Represents a single version creation trigger for the automation run.
/// </summary>
public sealed class VersionCreationTrigger : AutomationRunTriggerBase
{
  public const string VERSION_CREATION_TRIGGER_TYPE = "versionCreation";
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
  public required string ModelId { get; init; }
  public required string VersionId { get; init; }
}
