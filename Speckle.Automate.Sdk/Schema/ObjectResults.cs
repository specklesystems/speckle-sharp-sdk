namespace Speckle.Automate.Sdk.Schema;

public readonly struct ObjectResults
{
  public int Version => 1;
  public ObjectResultValues Values { get; init; }
}
