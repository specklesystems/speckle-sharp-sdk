using System.Reflection;
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

  public static void Initialize(params Assembly[] assemblies)
  {
    if (!s_initialized)
    {
      lock (s_availableTypes)
      {
        if (!s_initialized)
        {
          Load(assemblies);
          s_initialized = true;
        }
      }
    }
  }

  //Don't use unless you're testing
  public static void Reset()
  {
    s_availableTypes = new();
    s_initialized = false;
  }

  private static void Load(Assembly[] assemblies)
  {
    foreach (var assembly in assemblies.Distinct())
    {
      if (assembly.IsDynamic || assembly.ReflectionOnly)
      {
        continue;
      }

      foreach (var type in assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(Base)) && !t.IsAbstract))
      {
        s_availableTypes.Add(type);
      }
    }
  }
}
