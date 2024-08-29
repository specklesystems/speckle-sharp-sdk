using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Serialization;
using Speckle.Sdk.Host;

namespace Speckle.Sdk.Serialisation.Utilities;

internal static class TypeCache
{
  #region Getting Types
  private static ConcurrentDictionary<string, IReadOnlyDictionary<string, PropertyInfo>> s_typeProperties = new();
  private static ConcurrentDictionary<string, IReadOnlyList<MethodInfo>> s_onDeserializedCallbacks = new();

  internal static IReadOnlyDictionary<string, PropertyInfo> GetTypeProperties(string objFullType) =>
    s_typeProperties.GetOrAdd(
      objFullType,
      s =>
      {
        Type type = TypeLoader.GetType(s);
        PropertyInfo[] properties = type.GetProperties();
        Dictionary<string, PropertyInfo> ret = new(properties.Length, StringComparer.OrdinalIgnoreCase);
        foreach (PropertyInfo prop in properties)
        {
          ret[prop.Name] = prop;
        }
        return ret;
      }
    );

  internal static IReadOnlyList<MethodInfo> GetOnDeserializedCallbacks(string objFullType) =>
    s_onDeserializedCallbacks.GetOrAdd(
      objFullType,
      s =>
      {
        List<MethodInfo>? ret = null;
        Type type = TypeLoader.GetType(s);
        MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (MethodInfo method in methods)
        {
          if (method.IsDefined(typeof(OnDeserializedAttribute), true))
          {
            if (ret == null)
            {
              ret = new List<MethodInfo>();
            }
            ret.Add(method);
          }
        }
        return (ret as IReadOnlyList<MethodInfo>) ?? Array.Empty<MethodInfo>();
      }
    );

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
