using System.Collections.Concurrent;
using System.Reflection;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Common;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.Utilities;

namespace Speckle.Sdk.Serialisation;

public record Deserialized(string Id, string Json, Base BaseObject);

public class DeserializeStage
{
  /// <summary>
  /// Property that describes the type of the object.
  /// </summary>
  private const string TYPE_DISCRIMINATOR = nameof(Base.speckle_type);
  private readonly ConcurrentDictionary<string, IReadOnlyList<string>> _closures = new();
  public string? BlobStorageFolder { get; set; }
  private readonly object?[] _invokeNull = [null];

  public ReceiveStage? ReceiveStage { get; set; }

  public async ValueTask<Deserialized?> Execute(Transported message)
  {
    if (!_closures.TryGetValue(message.Id, out var closures))
    {
      closures = (await ClosureParser.GetChildrenIdsAsync(message.Json).ConfigureAwait(false)).ToList();
      _closures.TryAdd(message.Id, closures);
    }

    var closureBases = new Dictionary<string, Base>();
    bool anyNotFound = false;
    foreach (var c in closures)
    {
      if (ReceiveStage.NotNull().Cache.TryGetValue(c, out var cached))
      {
        closureBases.Add(c, cached);
      }
      else
      {
        await ReceiveStage.SourceChannel.Writer.WriteAsync(c).ConfigureAwait(false);
        anyNotFound = true;
      }
    }

    if (anyNotFound)
    {
      await ReceiveStage.NotNull().SourceChannel.Writer.WriteAsync(message.Id).ConfigureAwait(false);
      return null;
    }
    else
    {
      var @base = await Deserialise(closureBases, message.Id, message.Json).ConfigureAwait(false);
      _closures.TryRemove(message.Id, out _);
      return new(message.Id, message.Json, @base);
    }
  }

  private async ValueTask<Base> Deserialise(IReadOnlyDictionary<string, Base> dictionary, string id, string json)
  {
    if (ReceiveStage?.Cache.TryGetValue(id, out var baseObject) ?? false)
    {
      return baseObject;
    }
    SpeckleObjectDeserializer2 _deserializer = new(dictionary);
    var dict = await _deserializer.DeserializeJsonAsync(json).ConfigureAwait(false);
    return Dict2Base(dict);
  }

  private Base Dict2Base(Dictionary<string, object?> dictObj)
  {
    string typeName = (string)dictObj[TYPE_DISCRIMINATOR].NotNull();
    Type type = TypeLoader.GetType(typeName);
    Base baseObj = (Base)Activator.CreateInstance(type).NotNull();

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
