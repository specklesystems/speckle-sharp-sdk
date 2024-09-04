#nullable disable
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.RegularExpressions;
using Speckle.Newtonsoft.Json;
using Speckle.Newtonsoft.Json.Linq;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Host;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Models;

/// <summary>
/// Base class for all Speckle object definitions. Provides unified hashing, type extraction and serialisation.
/// <para>When developing a speckle kit, use this class as a parent class.</para>
/// <para><b>Dynamic properties naming conventions:</b></para>
/// <para>👉 "__" at the start of a property means it will be ignored, both for hashing and serialisation (e.g., "__ignoreMe").</para>
/// <para>👉 "@" at the start of a property name means it will be detached (when serialised with a transport) (e.g.((dynamic)obj)["@meshEquivalent"] = ...) .</para>
/// </summary>
[Serializable]
[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Serialized property names are camelCase by design")]
public class Base : DynamicBase, ISpeckleObject
{
  private static readonly Regex s_chunkSyntax = Constants.ChunkPropertyNameRegex;

  private string _type;

  /// <summary>
  /// A speckle object's id is an unique hash based on its properties. <b>NOTE: this field will be null unless the object was deserialised from a source. Use the <see cref="GetIdAsync"/> function to get it.</b>
  /// </summary>
  [SchemaIgnore]
  public virtual string id { get; set; }

#nullable enable //Starting nullability syntax here so that `id` null oblivious,

  /// <summary>
  /// Secondary, ideally host application driven, object identifier.
  /// </summary>
  [SchemaIgnore]
  public string? applicationId { get; set; }

  /// <summary>
  /// Holds the type information of this speckle object, derived automatically
  /// from its assembly name and inheritance.
  /// </summary>
  [SchemaIgnore]
  public virtual string speckle_type
  {
    get
    {
      if (_type == null)
      {
        _type = TypeLoader.GetFullTypeString(GetType());
      }
      return _type;
    }
  }

  /// <summary>
  /// Calculates the id (a unique hash) of this object.
  /// </summary>
  /// <remarks>
  /// This method fully serialize the object and any referenced objects. This has a tangible cost and should be avoided.<br/>
  /// Objects retrieved from a <see cref="ITransport"/> already have a <see cref="id"/> property populated<br/>
  /// The hash of a decomposed object differs from the hash of a non-decomposed object.
  /// </remarks>
  /// <param name="decompose">If <see langword="true"/>, will decompose the object in the process of hashing.</param>
  /// <returns>the resulting id (hash)</returns>
  public async Task<string> GetIdAsync(bool decompose = false)
  {
    //TODO remove me
    var transports = decompose ? [new MemoryTransport()] : Array.Empty<ITransport>();
    var serializer = new SpeckleObjectSerializer(transports);

    string obj = await serializer.SerializeAsync(this);
    return JObject.Parse(obj).GetValue(nameof(id))?.ToString() ?? string.Empty;
  }

  /// <summary>
  /// Attempts to count the total number of detachable objects.
  /// </summary>
  /// <returns>The total count of the detachable children + 1 (itself).</returns>
  public long GetTotalChildrenCount()
  {
    var parsed = new HashSet<int>();
    return 1 + CountDescendants(this, parsed);
  }

  private static long CountDescendants(Base @base, ISet<int> parsed)
  {
    if (parsed.Contains(@base.GetHashCode()))
    {
      return 0;
    }

    parsed.Add(@base.GetHashCode());

    long count = 0;
    var typedProps = @base.GetInstanceMembers();
    foreach (var prop in typedProps.Where(p => p.CanRead))
    {
      bool isIgnored =
        prop.IsDefined(typeof(ObsoleteAttribute), true) || prop.IsDefined(typeof(JsonIgnoreAttribute), true);
      if (isIgnored)
      {
        continue;
      }

      var detachAttribute = prop.GetCustomAttribute<DetachPropertyAttribute>(true);

      object? value = prop.GetValue(@base);

      if (detachAttribute is { Detachable: true })
      {
        var chunkAttribute = prop.GetCustomAttribute<ChunkableAttribute>(true);
        if (chunkAttribute == null)
        {
          count += HandleObjectCount(value, parsed);
        }
        else
        {
          // Simplified chunking count handling.
          if (value is IList asList)
          {
            count += asList.Count / chunkAttribute.MaxObjCountPerChunk;
          }
        }
      }
    }

    var dynamicProps = @base.DynamicPropertyKeys;
    foreach (var propName in dynamicProps)
    {
      if (!propName.StartsWith("@"))
      {
        continue;
      }

      // Simplfied dynamic prop chunking handling
      if (s_chunkSyntax.IsMatch(propName))
      {
        var match = s_chunkSyntax.Match(propName);
        _ = int.TryParse(match.Groups[^1].Value, out int chunkSize);

        if (chunkSize != -1 && @base[propName] is IList asList)
        {
          count += asList.Count / chunkSize;
          continue;
        }
      }

      count += HandleObjectCount(@base[propName], parsed);
    }

    return count;
  }

  private static long HandleObjectCount(object? value, ISet<int> parsed)
  {
    long count = 0;
    switch (value)
    {
      case Base b:
        count++;
        count += CountDescendants(b, parsed);
        return count;
      case IDictionary d:
      {
        foreach (DictionaryEntry kvp in d)
        {
          if (kvp.Value is Base b)
          {
            count++;
            count += CountDescendants(b, parsed);
          }
          else
          {
            count += HandleObjectCount(kvp.Value, parsed);
          }
        }

        return count;
      }
      case IEnumerable e
      and not string:
      {
        foreach (var arrValue in e)
        {
          if (arrValue is Base b)
          {
            count++;
            count += CountDescendants(b, parsed);
          }
          else
          {
            count += HandleObjectCount(arrValue, parsed);
          }
        }

        return count;
      }
      default:
        return count;
    }
  }

  /// <summary>
  /// Creates a shallow copy of the current base object.
  /// This operation does NOT copy/duplicate the data inside each prop.
  /// The new object's property values will be pointers to the original object's property value.
  /// </summary>
  /// <returns>A shallow copy of the original object.</returns>
  public Base ShallowCopy()
  {
    Type type = GetType();
    Base myDuplicate = (Base)Activator.CreateInstance(type);
    myDuplicate.id = id;
    myDuplicate.applicationId = applicationId;

    foreach (
      var kvp in GetMembers(
        DynamicBaseMemberType.Instance | DynamicBaseMemberType.Dynamic | DynamicBaseMemberType.SchemaIgnored
      )
    )
    {
      var propertyInfo = type.GetProperty(kvp.Key);
      if (propertyInfo is not null && !propertyInfo.CanWrite)
      {
        continue;
      }

      try
      {
        myDuplicate[kvp.Key] = kvp.Value;
      }
      catch (Exception ex) when (!ex.IsFatal())
      {
        // avoids any last ditch unsettable or strange props.
        SpeckleLog.Logger.Warning(
          "Shallow copy of {type} failed to copy {propertyName} of type {propertyType} with value {valueType} - CanWrite / CanRead",
          type,
          kvp.Key,
          propertyInfo?.PropertyType,
          kvp.Value?.GetType(),
          propertyInfo?.CanWrite,
          propertyInfo?.CanRead
        );
      }
    }

    return myDuplicate;
  }
}
