using Speckle.Core.Models;
using Speckle.Core.Transports;
using Speckle.Newtonsoft.Json;
using Speckle.Newtonsoft.Json.Linq;

namespace Speckle.Core.Serialisation;

public interface ISpeckleDeserializer<out RootType>
  where RootType : class
{
  CancellationToken CancellationToken { get; set; }

  /// <summary>
  /// The sync transport. This transport will be used synchronously.
  /// </summary>
  ITransport ReadTransport { get; set; }

  Action<string, int>? OnProgressAction { get; set; }
  string? BlobStorageFolder { get; set; }
  TimeSpan Elapsed { get; }
  int WorkerThreadCount { get; set; }

  /// <param name="rootObjectJson">The JSON string of the object to be deserialized <see cref="Base"/></param>
  /// <returns>A <see cref="Base"/> typed object deserialized from the <paramref name="rootObjectJson"/></returns>
  /// <exception cref="InvalidOperationException">Thrown when <see cref="BaseObjectDeserializerV2._isBusy"/></exception>
  /// <exception cref="ArgumentNullException"><paramref name="rootObjectJson"/> was null</exception>
  /// <exception cref="SpeckleDeserializeException"><paramref name="rootObjectJson"/> cannot be deserialised to type <see cref="Base"/></exception>
  // /// <exception cref="TransportException"><see cref="ReadTransport"/> did not contain the required json objects (closures)</exception>
  RootType Deserialize(string rootObjectJson);

  /// <param name="objectJson"></param>
  /// <returns>The deserialized object</returns>
  /// <exception cref="ArgumentNullException"><paramref name="objectJson"/> was null</exception>
  /// <exception cref="JsonReaderException "><paramref name="objectJson"/> was not valid JSON</exception>
  /// <exception cref="SpeckleDeserializeException">Failed to deserialize <see cref="JObject"/> to the target type</exception>
  object? DeserializeTransportObject(string objectJson);

  object? ConvertJsonElement(JToken doc);
}
