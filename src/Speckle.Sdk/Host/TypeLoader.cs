using System.Reflection;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.SerializationUtilities;

namespace Speckle.Sdk.Host;

public record LoadedType(string Name, Type Type, List<string> DeprecatedNames);

public static class TypeLoader
{
  private static bool s_initialized;
  private static List<LoadedType> s_availableTypes = new();

  /// <summary>
  /// Returns a list of all the types found in all the kits on this user's device.
  /// </summary>
  public static IReadOnlyList<LoadedType> Types
  {
    get
    {
      if (!s_initialized)
      {
        lock (s_availableTypes)
        {
          if (!s_initialized)
          {
            throw new InvalidOperationException(
              "Initialize with an assembly list has not be called.  Please use Initialize"
            );
          }
        }
      }
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
    BaseObjectSerializationUtilities.FlushCachedTypes();
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
        s_availableTypes.Add(ParseType(type));
      }
    }
  }

  public static LoadedType ParseType(Type type)
  {
    var speckleType = type.GetCustomAttribute<SpeckleTypeAttribute>();
    if (speckleType is null)
    {
      throw new InvalidOperationException($"{type.FullName} inherits from Base has no SpeckleTypeAttribute");
    }
    var deprecatedSpeckleTypes = type.GetCustomAttributes<DeprecatedSpeckleTypeAttribute>();
    return new LoadedType(speckleType.Name, type, deprecatedSpeckleTypes.Select(x => x.Name).ToList());
  }
}
