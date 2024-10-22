using Speckle.Sdk.Models;

namespace Speckle.Sdk.Serialisation.V2.Send;

public class SpeckleBaseChildFinder(ISpeckleBasePropertyGatherer propertyGatherer)
{
  public IEnumerable<string> GetChildIds(Base obj)
  {
    var props = propertyGatherer.ExtractAllProperties(obj);
    foreach (var kvp in props)
    {
      if (kvp.Value.value is Base child)
      {
        yield return child.id;
      }
    }
  }
}
