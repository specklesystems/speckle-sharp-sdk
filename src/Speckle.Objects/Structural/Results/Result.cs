using Speckle.Objects.Structural.Loading;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Structural.Results;

[SpeckleType("Objects.Structural.Results.Result")]
public class Result : Base
{
  public Result() { }

  public Result(LoadCase resultCase, string? description = null)
  {
    this.resultCase = resultCase;
    this.description = description ?? "";
  }

  public Result(LoadCombination resultCase, string? description = null)
  {
    this.resultCase = resultCase;
    this.description = description ?? "";
  }

  [DetachProperty]
  public Base resultCase { get; set; } //loadCase or loadCombination

  public string permutation { get; set; } //for enveloped cases?
  public string description { get; set; } = "";
}
