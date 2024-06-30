using System.Collections.Immutable;
using System.Reflection;

namespace Speckle.Core.Serialisation.TypeCache;

public sealed class CachedTypeInfo
{
  public string Key { get; private set; }
  public Type Type { get; private set; }
  public ImmutableDictionary<string, PropertyInfo> Props { get; private set; }
  public ImmutableList<MethodInfo> Callbacks { get; private set; }

  public CachedTypeInfo(
          string key,
          Type type,
          Dictionary<string, PropertyInfo> props,
          List<MethodInfo> callbacks)
  {
    Key = key;
    Type = type;
    Props = props.ToImmutableDictionary();
    Callbacks = callbacks.ToImmutableList();
  }
}
