using System.Diagnostics.CodeAnalysis;
using Speckle.Sdk.Models.Data;

namespace Speckle.Sdk.Models.Collections;

/// <summary>
/// Root collection that represents the top-level commit object.
/// Extends Collection to include model-wide properties that apply to the entire model.
/// </summary>
[SpeckleType("Speckle.Core.Models.Collections.RootCollection")]
public class RootCollection : Collection, IProperties
{
  public RootCollection() { }

  [SetsRequiredMembers] //need to be careful when making changes to this class that this constructor does actually set all required members, there's no analyser to double check
  public RootCollection(string name)
    : base(name)
  {
    properties = new();
  }

  /// <summary>
  /// Model-wide properties that apply to the entire model.
  /// </summary>
  public required Dictionary<string, object?> properties { get; set; }
}
