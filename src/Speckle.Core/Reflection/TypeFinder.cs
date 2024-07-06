using System.Reflection;

namespace Speckle.Core.Reflection;

public class TypeFinder : ITypeFinder
{
  public IList<Type> GetTypesWhereSubclassOf(IEnumerable<Assembly> assemblies, Type subclassOf)
  {
    List<Type> types = new();
    
    // this assumes the DUI2 objects are not already loaded
    foreach (var assembly in assemblies)
    {
      try
      {
        types.AddRange(assembly.GetTypes().Where(x => x.IsSubclassOf(subclassOf) && !x.IsAbstract));
      }
      catch (TypeLoadException)
      {
        // POC: guard against loading things that cause explosions
      }
    }

    return types;
  }
}
