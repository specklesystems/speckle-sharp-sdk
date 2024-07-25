using System.Collections.Immutable;
using System.Reflection;

namespace Speckle.Core.Serialisation.TypeCache;

// this can probably become a record tbf
public sealed class CachedTypeInfo
{
  public string UnversionedTypeName { get; private set; }
  public Type Type { get; private set; }
  public ImmutableDictionary<string, PropertyInfo> Props { get; private set; }
  public ImmutableList<MethodInfo> Callbacks { get; private set; }

  public CachedTypeInfo(
          string unversionedTypeName,
          Type type,
          Dictionary<string, PropertyInfo> props,
          List<MethodInfo> callbacks)
  {
    UnversionedTypeName = unversionedTypeName;
    Type = type;
    Props = props.ToImmutableDictionary();
    Callbacks = callbacks.ToImmutableList();
  }
}
