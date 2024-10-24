using System.Collections;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;

namespace Speckle.Sdk.Serialisation.V2.Send;

[GenerateAutoInterface]
public class SpeckleBaseChildFinder(ISpeckleBasePropertyGatherer propertyGatherer) : ISpeckleBaseChildFinder
{
  public IEnumerable<Base> GetChildren(Base obj)
  {
    var props = propertyGatherer.ExtractAllProperties(obj);
    foreach (var kvp in props)
    {
      if (kvp.Value.value is Base child)
      {
        yield return child;
      }
      if (kvp.Value.value is ICollection c)
      {
        foreach (var childC in c)
        {
          if (childC is Base b)
          {
            yield return b;
          }
        }
      }
      if (kvp.Value.value  is IDictionary d)
      {
        foreach (DictionaryEntry de in d)
        {
          if (de.Value is Base b)
          {
            yield return b;
          }
        }
      }
    }
    if (obj is Collection speckleCollection)
    {
      foreach (var child in speckleCollection.elements)
      {
          yield return child;
      }
    }
    
  }
}
