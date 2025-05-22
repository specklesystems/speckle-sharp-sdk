namespace Speckle.Automate.Sdk.Schema;

public readonly struct ObjectResultValues
{
  public required List<ResultCase> ObjectResults { get; init; }
  public required List<string> BlobIds { get; init; }
}
