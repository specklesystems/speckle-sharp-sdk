using System.Dynamic;
using System.Reflection;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Common;
using Speckle.Sdk.Host;

namespace Speckle.Sdk.Models;

/// <summary>
/// Base class implementing a bunch of nice dynamic object methods, like adding and removing props dynamically. Makes c# feel like json.
/// <para>Orginally adapted from Rick Strahl 🤘</para>
/// <para>https://weblog.west-wind.com/posts/2012/feb/08/creating-a-dynamic-extensible-c-expando-object</para>
/// </summary>
public class DynamicBase : DynamicObject, IDynamicMetaObjectProvider
{
  /// <summary>
  /// Default <see cref="DynamicBaseMemberType"/> value for <see cref="GetMembers"/>
  /// </summary>
  public const DynamicBaseMemberType DEFAULT_INCLUDE_MEMBERS =
    DynamicBaseMemberType.Instance | DynamicBaseMemberType.Dynamic;

  /// <summary>
  /// The actual property bag, where dynamically added props are stored.
  /// </summary>
  private readonly Dictionary<string, object?> _properties = new();

  /// <summary>
  /// Sets and gets properties using the key accessor pattern.
  /// </summary>
  /// <example>
  /// <c>myObject["superProperty"] = 42;</c>
  /// </example>
  /// <param name="key"></param>
  /// <returns></returns>
  [IgnoreTheItem]
  public object? this[string key]
  {
    get
    {
      if (_properties.TryGetValue(key, out object? value))
      {
        return value;
      }

      var pinfos = TypeLoader.GetBaseProperties(GetType());
      var prop = pinfos.FirstOrDefault(p => p.Name == key);

      if (prop == null)
      {
        return null;
      }

      return prop.GetValue(this);
    }
    set
    {
      if (!IsPropNameValid(key, out string reason))
      {
        throw new InvalidPropNameException(key, reason);
      }

      if (_properties.ContainsKey(key))
      {
        _properties[key] = value;
        return;
      }

      var pinfos = TypeLoader.GetBaseProperties(GetType());
      var prop = pinfos.FirstOrDefault(p => p.Name == key);

      if (prop == null)
      {
        _properties[key] = value;
        return;
      }

      try
      {
        prop.SetValue(this, value);
      }
      catch (Exception ex) when (!ex.IsFatal())
      {
        throw new SpeckleException($"Failed to set value for {GetType().Name}.{prop.Name}", ex);
      }
    }
  }

  /// <inheritdoc />
  /// <summary>
  /// Gets properties via the dot syntax.
  /// <para><c>((dynamic)myObject).superProperty;</c></para>
  /// </summary>
  /// <returns></returns>
  public override bool TryGetMember(GetMemberBinder binder, out object? result)
  {
    return _properties.TryGetValue(binder.Name, out result);
  }

  /// <summary>
  /// Sets properties via the dot syntax.
  /// <para><pre>((dynamic)myObject).superProperty = something;</pre></para>
  /// </summary>
  /// <param name="binder"></param>
  /// <param name="value"></param>
  /// <returns></returns>
  public override bool TrySetMember(SetMemberBinder binder, object? value)
  {
    var valid = IsPropNameValid(binder.Name, out _);
    if (valid)
    {
      _properties[binder.Name] = value;
    }

    return valid;
  }

  private static readonly HashSet<char> s_disallowedPropNameChars = new() { '.', '/' };

  public static string RemoveDisallowedPropNameChars(string name)
  {
    foreach (char c in s_disallowedPropNameChars)
    {
      name = name.Replace(c, ' ');
    }

    return name;
  }

  //apparently used a lot so optimize the check
  public unsafe bool IsPropNameValid(string name, out string reason)
  {
    if (string.IsNullOrEmpty(name) || name.Equals("@", StringComparison.Ordinal))
    {
      reason = "Found empty prop name";
      return false;
    }

    if (name.StartsWith("@@", StringComparison.Ordinal))
    {
      reason = "Only one leading '@' char is allowed. This signals the property value should be detached.";
      return false;
    }

    int len = name.Length;
    fixed (char* ptr = name)
    {
      for (int i = 0; i < len; i++)
      {
        if (s_disallowedPropNameChars.Contains(ptr[i]))
        {
          reason = $"Prop with name '{name}' contains invalid characters. The following characters are not allowed: ./";
          return false;
        }
      }
      // talk to ptr[0] etc; DO NOT go outside of ptr[0] <---> ptr[len-1]
    }
    reason = string.Empty;
    return true;
  }

  /// <summary>
  /// Gets all of the property names on this class, dynamic or not.
  /// </summary> <returns></returns>
  [Obsolete("Use `GetMembers(DynamicBaseMemberType.All).Keys` instead")]
  public override IEnumerable<string> GetDynamicMemberNames()
  {
    var pinfos = TypeLoader.GetBaseProperties(GetType());

    foreach (var pinfo in pinfos)
    {
      yield return pinfo.Name;
    }

    foreach (var kvp in _properties)
    {
      yield return kvp.Key;
    }
  }

  public static IEnumerable<string> GetInstanceMembersNames(Type t)
  {
    var pinfos = TypeLoader.GetBaseProperties(t);

    foreach (var pinfo in pinfos)
    {
      yield return pinfo.Name;
    }
  }

  /// <summary>
  /// Gets the defined (typed) properties of this object.
  /// </summary>
  /// <returns></returns>
  public IEnumerable<PropertyInfo> GetInstanceMembers()
  {
    return GetInstanceMembers(GetType());
  }

  public static IEnumerable<PropertyInfo> GetInstanceMembers(Type t)
  {
    var pinfos = TypeLoader.GetBaseProperties(t);
    foreach (var pinfo in pinfos)
    {
      if (pinfo.Name != "Item")
      {
        yield return pinfo;
      }
    }
  }

  /// <summary>
  ///  Gets the typed and dynamic properties.
  /// </summary>
  /// <param name="includeMembers">Specifies which members should be included in the resulting dictionary. Can be concatenated with "|"</param>
  /// <returns>A dictionary containing the key's and values of the object.</returns>
  public Dictionary<string, object?> GetMembers(DynamicBaseMemberType includeMembers = DEFAULT_INCLUDE_MEMBERS)
  {
    // Initialize an empty dict
    var dic = new Dictionary<string, object?>();

    // Add dynamic members
    if (includeMembers.HasFlag(DynamicBaseMemberType.Dynamic))
    {
      dic = new Dictionary<string, object?>(_properties);
    }

    if (includeMembers.HasFlag(DynamicBaseMemberType.Instance))
    {
      var pinfos = TypeLoader
        .GetBaseProperties(GetType())
        .Where(x =>
        {
          var hasIgnored = x.IsDefined(typeof(SchemaIgnoreAttribute), true);
          var hasObsolete = x.IsDefined(typeof(ObsoleteAttribute), true);

          // If obsolete is false and prop has obsolete attr
          // OR
          // If schemaIgnored is true and prop has schemaIgnore attr
          return !(
            !includeMembers.HasFlag(DynamicBaseMemberType.SchemaIgnored) && hasIgnored
            || !includeMembers.HasFlag(DynamicBaseMemberType.Obsolete) && hasObsolete
          );
        });
      foreach (var pi in pinfos)
      {
        if (!dic.ContainsKey(pi.Name)) //todo This is a TEMP FIX FOR #1969, and should be reverted after a proper fix is made!
        {
          dic.Add(pi.Name, pi.GetValue(this));
        }
      }
    }

    if (includeMembers.HasFlag(DynamicBaseMemberType.SchemaComputed))
    {
      GetType()
        .GetMethods()
        .Where(e => e.IsDefined(typeof(SchemaComputedAttribute)) && !e.IsDefined(typeof(ObsoleteAttribute)))
        .ToList()
        .ForEach(e =>
        {
          var attr = e.GetCustomAttribute<SchemaComputedAttribute>().NotNull();
          dic[attr.Name] = e.Invoke(this, null);
        });
    }

    return dic;
  }

  /// <summary>
  /// Gets the dynamically added property names only.
  /// </summary>
  /// <returns></returns>
  [JsonIgnore]
  public IReadOnlyCollection<string> DynamicPropertyKeys => _properties.Keys;
}

/// <summary>
/// This attribute is used internally to hide the this[key]{get; set;} property from inner reflection on members.
/// For more info see this discussion: https://speckle.community/t/why-do-i-keep-forgetting-base-objects-cant-use-item-as-a-dynamic-member/3246/5
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
internal sealed class IgnoreTheItemAttribute : Attribute { }
