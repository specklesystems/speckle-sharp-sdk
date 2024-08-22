namespace Speckle.Sdk.Models;

public interface ISpeckleObject
{
#nullable disable
  public string id { get; }

#nullable enable //Starting nullability syntax here so that `id` null oblivious,

  public string? applicationId { get; }

  public string speckle_type { get; }
}
