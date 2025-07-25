using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Speckle.Newtonsoft.Json;
using Speckle.Newtonsoft.Json.Linq;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Host;
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
  private string? _type;

  /// <summary>
  /// A speckle object's id is an unique hash based on its properties. <b>NOTE: this field will be null unless the object was deserialised from a source. Use the <see cref="GetId"/> function to get it.</b>
  /// </summary>
  public virtual string? id { get; set; }

  /// <summary>
  /// Secondary, ideally host application driven, object identifier.
  /// </summary>
  public string? applicationId { get; set; }

  /// <summary>
  /// Holds the type information of this speckle object, derived automatically
  /// from its assembly name and inheritance.
  /// </summary>
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
  public string GetId(bool decompose = false)
  {
    //TODO remove me
    var transports = decompose ? [new MemoryTransport()] : Array.Empty<ITransport>();
    var serializer = new SpeckleObjectSerializer(transports);

    string obj = serializer.Serialize(this);
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
    if (!parsed.Add(@base.GetHashCode()))
    {
      return 0;
    }

    long count = 0;
    var typedProps = @base.GetInstanceMembers();
    foreach (var prop in typedProps.Where(p => p.CanRead))
    {
      bool isIgnored = TypeLoader.IsObsolete(prop) || prop.IsDefined(typeof(JsonIgnoreAttribute), true);
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
      if (!PropNameValidator.IsDetached(propName))
      {
        continue;
      }

      // Simplfied dynamic prop chunking handling
      if (PropNameValidator.IsChunkable(propName, out int chunkSize))
      {
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
}
