using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.TeklaStructures;

public class TeklaModel : Base
{
  [DetachProperty]
  public List<Base> Beams { get; set; }

  [DetachProperty]
  public List<Base> Rebars { get; set; }
}
