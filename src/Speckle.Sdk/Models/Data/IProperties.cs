namespace Speckle.Sdk.Models.Data;

/// <summary>
/// Specifies properties on objects to be used for data-based workflows.
/// Can be applied to both objects and collections.
/// </summary>
public interface IProperties
{
  Dictionary<string, object?> properties { get; }
}
