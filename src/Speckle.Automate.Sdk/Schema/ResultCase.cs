namespace Speckle.Automate.Sdk.Schema;

public readonly struct ResultCase
{
  public required string Category { get; init; }
  public required string Level { get; init; }
  public required Dictionary<string, string?> ObjectAppIds { get; init; }
  public required string? Message { get; init; }
  public required Dictionary<string, object>? Metadata { get; init; }
  public required Dictionary<string, object>? VisualOverrides { get; init; }
}
