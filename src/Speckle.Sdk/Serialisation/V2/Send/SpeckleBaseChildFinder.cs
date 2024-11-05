﻿using System.Collections;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Serialisation.V2.Send;

[GenerateAutoInterface]
public class SpeckleBaseChildFinder(ISpeckleBasePropertyGatherer propertyGatherer) : ISpeckleBaseChildFinder
{
  public IEnumerable<Base> GetChildren(Base obj)
  {
    var props = propertyGatherer.ExtractAllProperties(obj);
    foreach (var kvp in props.Where(x => x.PropertyAttributeInfo.IsDetachable))
    {
      if (kvp.Value is Base child)
      {
        yield return child;
      }
      if (kvp.Value is ICollection c)
      {
        foreach (var childC in c)
        {
          if (childC is Base b)
          {
            yield return b;
          }
        }
      }
      if (kvp.Value is IDictionary d)
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
  }
}
