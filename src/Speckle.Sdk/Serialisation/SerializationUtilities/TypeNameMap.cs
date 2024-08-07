using System.Collections.Concurrent;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Serialisation.SerializationUtilities;

public static class TypeNameMap
{
  private static Dictionary<string, Type> s_cachedTypes = new();
  private static ConcurrentDictionary<Type, string> s_fullTypeStrings = new();

  public static Type GetType(string objFullType)
  {
    lock (s_cachedTypes)
    {
      if (s_cachedTypes.TryGetValue(objFullType, out Type? type1))
      {
        return type1;
      }

      var type = GetAtomicType(objFullType);
      s_cachedTypes[objFullType] = type;
      return type;
    }
  }

  public static string GetFullTypeString(Type type) =>
    s_fullTypeStrings.GetOrAdd(
      type,
      t =>
      {
        Stack<string> bases = new();
        Type? myType = t;

        do
        {
          if (!myType.IsAbstract)
          {
            var typeString = GetTypeString(myType);
            if (typeString is null)
            {
              break;
            }
            bases.Push(typeString);
          }

          myType = myType.BaseType;
        } while (myType is not null && myType.Name != nameof(Base));

        if (bases.Count == 0)
        {
          return nameof(Base);
        }
        return string.Join(":", bases);
      }
    );

  public static string? GetTypeString(Type type)
  {
    var typeInfo = TypeLoader.Types.FirstOrDefault(tp => tp.Type == type);
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
      var type = TypeLoader.Types.FirstOrDefault(tp =>
        tp.Name == typeName || tp.DeprecatedNames.Any(x => x == typeName)
      );
      if (type != null)
      {
        return type.Type;
      }
    }

    return typeof(Base);
  }

  public static void Reset()
  {
    lock (s_cachedTypes)
    {
      s_cachedTypes = new();
      s_fullTypeStrings = new();
    }
  }
}
