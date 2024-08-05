using System.Reflection;
using System.Runtime.Serialization;
using Speckle.Core.Host;
using Speckle.Core.Models;

namespace Speckle.Core.Serialisation.SerializationUtilities;

internal static class BaseObjectSerializationUtilities
{
  #region Getting Types

  private static Dictionary<string, Type> s_cachedTypes = new();

  private static readonly Dictionary<string, Dictionary<string, PropertyInfo>> s_typeProperties = new();

  private static readonly Dictionary<string, List<MethodInfo>> s_onDeserializedCallbacks = new();

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

  internal static Type GetAtomicType(string objFullType)
  {
    var objectTypes = objFullType.Split(':').Reverse();
    foreach (var typeName in objectTypes)
    {
      //TODO: rather than getting the type from the first loaded kit that has it, maybe
      //we get it from a specific Kit
      var type = TypeLoader.Types.FirstOrDefault(tp => tp.FullName == typeName);
      if (type != null)
      {
        return type;
      }

      //To allow for backwards compatibility saving deserialization target types.
      //We also check a ".Deprecated" prefixed namespace
      string deprecatedTypeName = GetDeprecatedTypeName(typeName);

      var deprecatedType = TypeLoader.Types.FirstOrDefault(tp => tp.FullName == deprecatedTypeName);
      if (deprecatedType != null)
      {
        return deprecatedType;
      }
    }

    return typeof(Base);
  }

  internal static string GetDeprecatedTypeName(string typeName, string deprecatedSubstring = "Deprecated.")
  {
    int lastDotIndex = typeName.LastIndexOf('.');
    return typeName.Insert(lastDotIndex + 1, deprecatedSubstring);
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
      s_cachedTypes = new Dictionary<string, Type>();
    }
  }

  #endregion
}
