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
      // POC: right one? more?
      catch (ReflectionTypeLoadException)
      {
        // POC: guard against loading things that cause explosions due to not being able to load assemblies but are not real issues
      }
    }

    return types;
  }
  
  public IList<Type> GetTypesWhereImplementing(IEnumerable<Assembly> assemblies, Type subclassOf)
  {
    List<Type> types = new();
    
    // this assumes the DUI2 objects are not already loaded
    foreach (var assembly in assemblies)
    {
      try
      {
        types.AddRange(assembly.GetTypes().Where(x => x.GetInterfaces().Contains(subclassOf) && !x.IsAbstract));
      }
      // POC: right one? more?
      catch (ReflectionTypeLoadException)
      {
        // POC: guard against loading things that cause explosions due to not being able to load assemblies but are not real issues
      }
    }

    return types;
  }
}
