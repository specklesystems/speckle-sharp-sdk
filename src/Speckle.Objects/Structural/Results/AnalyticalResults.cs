using Speckle.Sdk.Models;

namespace Speckle.Objects.Structural.Results;

public class AnalyticalResults : Base
{
  public string? lengthUnits { get; set; }
  public string? forceUnits { get; set; }

  [DetachProperty]
  public List<Result> resultsByLoadCombination { get; set; }
}
