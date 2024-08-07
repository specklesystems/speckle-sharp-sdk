using System.Reflection;
using System.Runtime.Serialization;
using Speckle.Sdk.Host;

namespace Speckle.Sdk.Serialisation.SerializationUtilities;

internal static class BaseObjectSerializationUtilities
{
  #region Getting Types
  private static Dictionary<string, Dictionary<string, PropertyInfo>> s_typeProperties = new();
  private static Dictionary<string, List<MethodInfo>> s_onDeserializedCallbacks = new();

  internal static Dictionary<string, PropertyInfo> GetTypeProperties(string objFullType)
  {
    lock (s_typeProperties)
    {
      if (s_typeProperties.TryGetValue(objFullType, out Dictionary<string, PropertyInfo>? value))
      {
        return value;
      }

      Dictionary<string, PropertyInfo> ret = new();
      Type type = TypeLoader.GetType(objFullType);
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
      Type type = TypeLoader.GetType(objFullType);
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

  /// <summary>
  /// Flushes kit's (discriminator, type) cache. Useful if you're dynamically loading more kits at runtime, that provide better coverage of what you're deserialising, and it's now somehow poisoned because the higher level types were not originally available.
  /// </summary>
  public static void FlushCachedTypes()
  {
    lock (s_typeProperties)
    {
      s_typeProperties = new();
      s_onDeserializedCallbacks = new();
    }
  }

  #endregion
}
