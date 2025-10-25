namespace Speckle.Sdk.Models.Collections;

/// <summary>
/// Root collection that represents the top-level commit object.
/// Extends Collection to include model-wide properties that apply to the entire model.
/// </summary>
[SpeckleType("Speckle.Core.Models.Collections.RootCollection")]
public class RootCollection : Collection
{
  /// <summary>
  /// Parameterless constructor for deserialization.
  /// </summary>
  public RootCollection() { }
  
  /// <summary>
  /// Constructor for a root collection.
  /// </summary>
  /// <param name="name">The human-readable name of this root collection</param>
  public RootCollection(string name) : base(name) { }

  /// <summary>
  /// Model-wide properties that apply to the entire model.
  /// </summary>
  /// <remarks>
  /// These are intended for model-level metadata such as total area, project information, or analysis results.
  /// </remarks>
  public Dictionary<string, object?>? rootProperties { get; set; }
}
