using System.Reflection;
using System.Threading.Channels;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Common;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.Utilities;

namespace Speckle.Sdk.Serialisation;

public record Deserialized(string Id, string Json, Base BaseObject);
public class DeserializeStage : Stage<Transported, Deserialized>
{ 

  public string? BlobStorageFolder { get; set; }
  private readonly object?[] _invokeNull = [null];

  /// <summary>
  /// Property that describes the type of the object.
  /// </summary>
  private const string TYPE_DISCRIMINATOR = nameof(Base.speckle_type);
  
  private readonly SpeckleObjectDeserializer _deserializer = new ();
  public DeserializeStage()
    : base(Channel.CreateUnbounded<Transported>())
  {
  }
  
  public ReceiveStage? ReceiveStage { get; set; }

  protected override async ValueTask<Deserialized?> Execute(Transported message)
  {
  }

  private async ValueTask<Base> Deserialiser(string id, string json)
  {
    if (ReceiveStage?.Cache.TryGetValue(id, out var baseObject) ?? false)
    {
      return baseObject;
    }
    var dict = await _deserializer.DeserializeJsonAsync(message.Json).ConfigureAwait(false);
    if (dict.TryGetValue(TYPE_DISCRIMINATOR, out object? speckleType))
    {
      if (speckleType as string == "reference" && dict.TryGetValue("referencedId", out object? referencedId))
      {
        var objId = (string)referencedId.NotNull();
        object? deserialized = await TryGetDeserializedAsync(objId).ConfigureAwait(false);
        return deserialized;
      }
      return dict;
    }
  }
  
  
  private Base Dict2Base(Dictionary<string, object?> dictObj)
  {
    string typeName = (string)dictObj[TYPE_DISCRIMINATOR].NotNull();
    Type type = TypeLoader.GetType(typeName);
    Base baseObj = (Base)Activator.CreateInstance(type);

    dictObj.Remove(TYPE_DISCRIMINATOR);
    dictObj.Remove("__closure");

    var staticProperties = TypeCache.GetTypeProperties(typeName);
    foreach (var entry in dictObj)
    {
      if (staticProperties.TryGetValue(entry.Key, out PropertyInfo? value) && value.CanWrite)
      {
        if (entry.Value == null)
        {
          // Check for JsonProperty(NullValueHandling = NullValueHandling.Ignore) attribute
          JsonPropertyAttribute? attr = TypeLoader.GetJsonPropertyAttribute(value);
          if (attr is { NullValueHandling: NullValueHandling.Ignore })
          {
            continue;
          }
        }

        Type targetValueType = value.PropertyType;
        bool conversionOk = ValueConverter.ConvertValue(targetValueType, entry.Value, out object? convertedValue);
        if (conversionOk)
        {
          value.SetValue(baseObj, convertedValue);
        }
        else
        {
          // Cannot convert the value in the json to the static property type
          throw new SpeckleDeserializeException(
            $"Cannot deserialize {entry.Value?.GetType().FullName} to {targetValueType.FullName}"
          );
        }
      }
      else
      {
        // No writable property with this name
        CallSiteCache.SetValue(entry.Key, baseObj, entry.Value);
      }
    }

    if (baseObj is Blob bb && BlobStorageFolder != null)
    {
      bb.filePath = bb.GetLocalDestinationPath(BlobStorageFolder);
    }

    var onDeserializedCallbacks = TypeCache.GetOnDeserializedCallbacks(typeName);
    foreach (MethodInfo onDeserialized in onDeserializedCallbacks)
    {
      onDeserialized.Invoke(baseObj, _invokeNull);
    }

    return baseObj;
  }
}
