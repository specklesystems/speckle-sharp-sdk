using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Structural.Loading;

[SpeckleType("Objects.Structural.Loading.LoadCombination")]
public class LoadCombination : Base //combination case
{
  public LoadCombination() { }

  /// <summary>
  ///
  /// </summary>
  /// <param name="name"></param>
  /// <param name="loadCases"></param>
  /// <param name="loadFactors"></param>
  /// <param name="combinationType"></param>
  [SchemaInfo("Load Combination", "Creates a Speckle load combination", "Structural", "Loading")]
  public LoadCombination(
    string name,
    [SchemaParamInfo("A list of load cases")] List<Base> loadCases,
    [SchemaParamInfo("A list of load factors (to be mapped to provided load cases)")] List<double> loadFactors,
    CombinationType combinationType
  )
  {
    if (loadCases.Count != loadFactors.Count)
    {
      throw new ArgumentException("Number of load cases provided does not match number of load factors provided");
    }

    this.name = name;
    this.loadCases = loadCases;
    this.loadFactors = loadFactors;
    this.combinationType = combinationType;
  }

  public string name { get; set; }

  [DetachProperty]
  public List<Base> loadCases { get; set; }

  public List<double> loadFactors { get; set; }
  public CombinationType combinationType { get; set; }
}
