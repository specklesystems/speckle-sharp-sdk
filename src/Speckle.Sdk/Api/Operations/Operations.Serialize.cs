using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;

namespace Speckle.Sdk.Api;

public static partial class Operations
{
  /// <summary>
  /// Serializes a given object.
  /// </summary>
  /// <remarks>
  /// If you want to save and persist an object to Speckle Transport or Server,
  /// please use any of the "Send" methods.
  /// <see cref="Send(Base,Speckle.Sdk.Transports.ITransport,bool,System.Action{System.Collections.Concurrent.ConcurrentDictionary{string,int}}?,System.Threading.CancellationToken)"/>
  /// </remarks>
  /// <param name="value">The object to serialise</param>
  /// <param name="cancellationToken"></param>
  /// <returns>A json string representation of the object.</returns>
  public static string Serialize(Base value, CancellationToken cancellationToken = default)
  {
    var serializer = new BaseObjectSerializerV2 { CancellationToken = cancellationToken };
    return serializer.Serialize(value);
  }

  /// <remarks>
  /// Note: if you want to pull an object from a Speckle Transport or Server,
  /// please use
  /// <see cref="Receive(string,Speckle.Sdk.Transports.ITransport?,Speckle.Sdk.Transports.ITransport?,System.Action{System.Collections.Concurrent.ConcurrentDictionary{string,int}}?,System.Action{int}?,System.Threading.CancellationToken)"/>
  /// </remarks>
  /// <param name="value">The json string representation of a speckle object that you want to deserialize</param>
  /// <param name="cancellationToken"></param>
  /// <returns><inheritdoc cref="BaseObjectDeserializerV2.Deserialize"/></returns>
  /// <exception cref="ArgumentNullException"><paramref name="value"/> was null</exception>
  /// <exception cref="JsonReaderException "><paramref name="value"/> was not valid JSON</exception>
  /// <exception cref="SpeckleException"><paramref name="value"/> cannot be deserialised to type <see cref="Base"/></exception>
  /// <exception cref="Speckle.Sdk.Transports.TransportException"><paramref name="value"/> contains closure references (see Remarks)</exception>
  public static Base Deserialize(string value, CancellationToken cancellationToken = default)
  {
    var deserializer = new BaseObjectDeserializerV2 { CancellationToken = cancellationToken };
    return deserializer.Deserialize(value);
  }
}
