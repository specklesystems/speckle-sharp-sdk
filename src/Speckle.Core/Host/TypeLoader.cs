using System.Reflection;
using Speckle.Core.Kits;
using Speckle.Core.Models;

namespace Speckle.Core.Host;

public static class TypeLoader
{  
  private static bool s_initialized;
  private static List<Type> s_availableTypes = new();
  /// <summary>
  /// Returns a list of all the types found in all the kits on this user's device.
  /// </summary>
  public static IReadOnlyList<Type> Types
  {
    get
    {
      Initialize();
      return s_availableTypes;
    }
  }
  private static void Initialize()
  {
    if (!s_initialized)
    {
      lock (s_availableTypes)
      {
        if (!s_initialized)
        {
          Load();
          s_initialized = true;
        }
      }
    }
  }

  private static void Load()
  {
    List<Assembly> assemblies = AppDomain.CurrentDomain.GetAssemblies().ToList();

    foreach (var assembly in assemblies)
    {
      if (assembly.IsDynamic || assembly.ReflectionOnly)
      {
        continue;
      }

      foreach (var type in assembly.GetTypes(). Where(t => t.IsSubclassOf(typeof(Base)) && !t.IsAbstract))
      {
        s_availableTypes.Add(type);
      }
    }
  }
}
