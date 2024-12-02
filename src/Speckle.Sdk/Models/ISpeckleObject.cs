namespace Speckle.Sdk.Models;

public interface ISpeckleObject
{
  public string? id { get; }
  
  public string? applicationId { get; }

  public string speckle_type { get; }
}
