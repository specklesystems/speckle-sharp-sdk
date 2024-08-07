using System.Collections.Concurrent;
using System.Reflection;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Host;

public record LoadedType(string Name, Type Type, List<string> DeprecatedNames);

public static class TypeLoader
{
  private static bool s_initialized;
  private static List<LoadedType> s_availableTypes = new();

  private static ConcurrentDictionary<string, Type> s_cachedTypes = new();
  private static ConcurrentDictionary<Type, string> s_fullTypeStrings = new();

  public static IEnumerable<LoadedType> Types => s_availableTypes;

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

  public static Type GetType(string fullTypeString) =>
    s_cachedTypes.GetOrAdd(
      fullTypeString,
      typeString =>
      {
        var type = GetAtomicType(typeString);
        s_cachedTypes[typeString] = type;
        return type;
      }
    );

  public static string GetFullTypeString(Type type) =>
    s_fullTypeStrings.GetOrAdd(
      type,
      t =>
      {
        Stack<string> bases = new();
        if (t == typeof(Base))
        {
          return nameof(Base);
        }
        Type? myType = t;

        do
        {
          if (!myType.IsAbstract)
          {
            var typeString = GetTypeString(myType);
            if (typeString is null)
            {
              throw new InvalidOperationException($"Type {t} is not registered with TypeLoader");
            }
            bases.Push(typeString);
          }

          myType = myType.BaseType;
        } while (myType is not null && myType.Name != nameof(Base));
        return string.Join(":", bases);
      }
    );

  public static string? GetTypeString(Type type)
  {
    var typeInfo = s_availableTypes.FirstOrDefault(tp => tp.Type == type);
    if (typeInfo != null)
    {
      return typeInfo.Name;
    }
    return null;
  }

  public static Type GetAtomicType(string objFullType)
  {
    var objectTypes = objFullType.Split(':').Reverse();
    foreach (var typeName in objectTypes)
    {
      var type = s_availableTypes.FirstOrDefault(tp =>
        tp.Name == typeName || tp.DeprecatedNames.Any(x => x == typeName)
      );
      if (type != null)
      {
        return type.Type;
      }
    }

    return typeof(Base);
  }

  //Don't use unless you're testing
  public static void Reset()
  {
    s_availableTypes = new();
    s_cachedTypes = new();
    s_fullTypeStrings = new();
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
