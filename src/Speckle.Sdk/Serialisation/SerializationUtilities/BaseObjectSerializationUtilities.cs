using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Serialization;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Serialisation.SerializationUtilities;

internal static class BaseObjectSerializationUtilities
{
  #region Getting Types

  private static Dictionary<string, Type> s_cachedTypes = new();
  private static ConcurrentDictionary<Type, string> s_fullTypeStrings = new();
  private static Dictionary<string, Dictionary<string, PropertyInfo>> s_typeProperties = new();
  private static Dictionary<string, List<MethodInfo>> s_onDeserializedCallbacks = new();

  internal static Type GetType(string objFullType)
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

  internal static string GetFullTypeString(Type type) =>
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

  internal static string? GetTypeString(Type type)
  {
    var typeInfo = TypeLoader.Types.FirstOrDefault(tp => tp.Type == type);
    if (typeInfo != null)
    {
      return typeInfo.Name;
    }
    return null;
  }

  internal static Type GetAtomicType(string objFullType)
  {
    var objectTypes = objFullType.Split(':').Reverse();
    foreach (var typeName in objectTypes)
    {
      //TODO: rather than getting the type from the first loaded kit that has it, maybe
      //we get it from a specific Kit
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

  internal static Dictionary<string, PropertyInfo> GetTypeProperties(string objFullType)
  {
    lock (s_typeProperties)
    {
      if (s_typeProperties.TryGetValue(objFullType, out Dictionary<string, PropertyInfo>? value))
      {
        return value;
      }

      Dictionary<string, PropertyInfo> ret = new();
      Type type = GetType(objFullType);
      PropertyInfo[] properties = type.GetProperties();
      foreach (PropertyInfo prop in properties)
      {
        ret[prop.Name.ToLower()] = prop;
      }

      value = ret;
      s_typeProperties[objFullType] = value;
      return value;
    }
  }

  internal static List<MethodInfo> GetOnDeserializedCallbacks(string objFullType)
  {
    // return new List<MethodInfo>();
    lock (s_onDeserializedCallbacks)
    {
      // System.Runtime.Serialization.Ca
      if (s_onDeserializedCallbacks.TryGetValue(objFullType, out List<MethodInfo>? value))
      {
        return value;
      }

      List<MethodInfo> ret = new();
      Type type = GetType(objFullType);
      MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
      foreach (MethodInfo method in methods)
      {
        List<OnDeserializedAttribute> onDeserializedAttributes = method
          .GetCustomAttributes<OnDeserializedAttribute>(true)
          .ToList();
        if (onDeserializedAttributes.Count > 0)
        {
          ret.Add(method);
        }
      }

      value = ret;
      s_onDeserializedCallbacks[objFullType] = value;
      return value;
    }
  }

  internal static Type GetSystemOrSpeckleType(string typeName)
  {
    var systemType = Type.GetType(typeName);
    if (systemType != null)
    {
      return systemType;
    }

    return GetAtomicType(typeName);
  }

  /// <summary>
  /// Flushes kit's (discriminator, type) cache. Useful if you're dynamically loading more kits at runtime, that provide better coverage of what you're deserialising, and it's now somehow poisoned because the higher level types were not originally available.
  /// </summary>
  public static void FlushCachedTypes()
  {
    lock (s_cachedTypes)
    {
      s_cachedTypes = new();
      s_fullTypeStrings = new();
      s_typeProperties = new();
      s_onDeserializedCallbacks = new();
    }
  }

  #endregion
}
