using System.Diagnostics.CodeAnalysis;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Api;

public partial class Operations
{
  /// <summary>
  /// Serializes a given object.
  /// </summary>
  /// <remarks>
  /// If you want to save and persist an object to Speckle Transport or Server,
  /// please use any of the "Send" methods.
  /// <see cref="Send(Base,Speckle.Sdk.Transports.ITransport,bool,System.Action{System.Collections.Concurrent.ConcurrentBag{ProgressArgs}}?,System.Threading.CancellationToken)"/>
  /// </remarks>
  /// <param name="value">The object to serialise</param>
  /// <param name="cancellationToken"></param>
  /// <returns>A json string representation of the object.</returns>
  public string Serialize(Base value, CancellationToken cancellationToken = default)
  {
    var serializer = new SpeckleObjectSerializer { CancellationToken = cancellationToken };
    return serializer.Serialize(value);
  }

  /// <remarks>
  /// Note: if you want to pull an object from a Speckle Transport or Server,
  /// please use
  /// <see cref="Receive(string,Speckle.Sdk.Transports.ITransport?,Speckle.Sdk.Transports.ITransport?,System.Action{System.Collections.Concurrent.ConcurrentBag{ProgressArgs}}?,System.Action{int}?,System.Threading.CancellationToken)"/>
  /// </remarks>
  /// <param name="value">The json string representation of a speckle object that you want to deserialize</param>
  /// <param name="cancellationToken"></param>
  /// <returns><inheritdoc cref="SpeckleObjectDeserializer.DeserializeAsync"/></returns>
  /// <exception cref="ArgumentNullException"><paramref name="value"/> was null</exception>
  /// <exception cref="JsonReaderException "><paramref name="value"/> was not valid JSON</exception>
  /// <exception cref="SpeckleException"><paramref name="value"/> cannot be deserialised to type <see cref="Base"/></exception>
  /// <exception cref="Speckle.Sdk.Transports.TransportException"><paramref name="value"/> contains closure references (see Remarks)</exception>
  public async Task<Base> DeserializeAsync(string value, CancellationToken cancellationToken = default)
  {
    var deserializer = new SpeckleObjectDeserializer { CancellationToken = cancellationToken };
    return await DeserializeActivity(value, deserializer).ConfigureAwait(false);
  }

  /// <inheritdoc cref="SpeckleObjectDeserializer.DeserializeAsync"/>
  private async Task<Base> DeserializeActivity([NotNull] string? objString, SpeckleObjectDeserializer deserializer)
  {
    using var activity = activityFactory.Start();
    try
    {
      Base res = await deserializer.DeserializeAsync(objString).ConfigureAwait(false);
      activity?.SetStatus(SdkActivityStatusCode.Ok);
      return res;
    }
    catch (Exception ex)
    {
      activity?.SetStatus(SdkActivityStatusCode.Error);
      activity?.RecordException(ex);
      throw;
    }
  }
}
