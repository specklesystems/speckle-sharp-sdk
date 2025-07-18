using System.Collections.Concurrent;
using System.Reflection;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Host;

internal record LoadedType(string Name, Type Type, List<string> DeprecatedNames);

internal static class TypeLoader
{
  private static bool s_initialized;
  private static List<LoadedType> s_availableTypes = new();

  private static ConcurrentDictionary<string, Type> s_cachedTypes = new();
  private static ConcurrentDictionary<Type, string> s_fullTypeStrings = new();
  private static ConcurrentDictionary<PropertyInfo, JsonPropertyAttribute?> s_jsonPropertyAttribute = new();
  private static readonly ConcurrentDictionary<PropertyInfo, bool> s_obsolete = new();
  private static ConcurrentDictionary<Type, IReadOnlyList<PropertyInfo>> s_propInfoCache = new();

  public static IEnumerable<LoadedType> Types => s_availableTypes;

  public static bool IsObsolete(PropertyInfo property) =>
    s_obsolete.GetOrAdd(property, p => p.IsDefined(typeof(ObsoleteAttribute), true));

  public static JsonPropertyAttribute? GetJsonPropertyAttribute(PropertyInfo property) =>
    s_jsonPropertyAttribute.GetOrAdd(property, p => p.GetCustomAttribute<JsonPropertyAttribute>(true));

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

  private static void CheckInitialized()
  {
    if (!s_initialized)
    {
      throw new InvalidOperationException("TypeLoader is not initialized.");
    }
  }

  public static IReadOnlyList<PropertyInfo> GetBaseProperties(Type type)
  {
    CheckInitialized();
    return s_propInfoCache.GetOrAdd(
      type,
      t =>
        t.GetProperties(BindingFlags.Instance | BindingFlags.Public)
          .Where(p => !p.IsDefined(typeof(IgnoreTheItemAttribute), true))
          .ToList()
    );
  }

  public static Type GetType(string fullTypeString)
  {
    CheckInitialized();
    return s_cachedTypes.GetOrAdd(
      fullTypeString,
      typeString =>
      {
        var type = GetAtomicType(typeString);
        s_cachedTypes[typeString] = type;
        return type;
      }
    );
  }

  public static string GetFullTypeString(Type type)
  {
    CheckInitialized();
    return s_fullTypeStrings.GetOrAdd(
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
  }

  public static string? GetTypeString(Type type)
  {
    CheckInitialized();
    var typeInfo = s_availableTypes.FirstOrDefault(tp => tp.Type == type);
    if (typeInfo != null)
    {
      return typeInfo.Name;
    }
    return null;
  }

  public static Type GetAtomicType(string objFullType)
  {
    CheckInitialized();
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
    s_jsonPropertyAttribute = new();
    s_propInfoCache = new();
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
    return new LoadedType(
      speckleType.SpeckleTypeName,
      type,
      deprecatedSpeckleTypes.Select(x => x.SpeckleTypeName).ToList()
    );
  }
}
