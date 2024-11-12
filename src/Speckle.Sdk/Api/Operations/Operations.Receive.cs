using Microsoft.Extensions.Logging;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Api;

public partial class Operations
{
  public async Task<Base> Receive2(
    Uri url,
    string streamId,
    string objectId,
    string? authorizationToken = null,
    IProgress<ProgressArgs>? onProgressAction = null,
    CancellationToken cancellationToken = default
  )
  {
    using var receiveActivity = activityFactory.Start("Operations.Receive");
    metricsFactory.CreateCounter<long>("Receive").Add(1);

    receiveActivity?.SetTag("objectId", objectId);

    try
    {
      var process = serializeProcessFactory.CreateDeserializeProcess(
        url,
        streamId,
        authorizationToken,
        onProgressAction
      );
      var result = await process.Deserialize(objectId, cancellationToken).ConfigureAwait(false);
      receiveActivity?.SetStatus(SdkActivityStatusCode.Ok);
      return result;
    }
    catch (Exception ex)
    {
      receiveActivity?.SetStatus(SdkActivityStatusCode.Error);
      receiveActivity?.RecordException(ex);
      throw;
    }
  }

  /// <summary>
  /// Receives an object (and all its sub-children) from the two provided <see cref="ITransport"/>s.
  /// <br/>
  /// Will first try and find objects using the <paramref name="localTransport"/> (the faster transport)
  /// If not found, will attempt to copy the objects from the <paramref name="remoteTransport"/> into the <paramref name="localTransport"/> before deserialization
  /// </summary>
  /// <remarks>
  /// If Transports are properly implemented, there is no hard distinction between what is a local or remote transport; it's still just an <see cref="ITransport"/>.
  /// <br/>So, for example, if you want to receive an object without actually writing it first to a local transport, you can just pass a <see cref="ServerTransport"/> as a local transport.
  /// <br/>This is not recommended, but shows what you can do. Another tidbit: the local transport does not need to be disk-bound; it can easily be an in <see cref="MemoryTransport"/>. In memory transports are the fastest ones, but they're of limited use for larger datasets
  /// </remarks>
  /// <param name="objectId">The id of the object to receive</param>
  /// <param name="remoteTransport">The remote transport (slower). If <see langword="null"/>, will assume all objects are present in <paramref name="localTransport"/></param>
  /// <param name="localTransport">The local transport (faster). If <see langword="null"/>, will use a default <see cref="SQLiteTransport"/> cache</param>
  /// <param name="onProgressAction">Action invoked on progress iterations</param>
  /// <param name="cancellationToken"></param>
  /// <exception cref="TransportException">Failed to retrieve objects from the provided transport(s)</exception>
  /// <exception cref="SpeckleDeserializeException">Deserialization of the requested object(s) failed</exception>
  /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> requested cancel</exception>
  /// <returns>The requested Speckle Object</returns>
  public async Task<Base> Receive(
    string objectId,
    ITransport? remoteTransport = null,
    ITransport? localTransport = null,
    IProgress<ProgressArgs>? onProgressAction = null,
    CancellationToken cancellationToken = default
  )
  {
    using var receiveActivity = activityFactory.Start("Operations.Receive");
    metricsFactory.CreateCounter<long>("Receive").Add(1);

    if (remoteTransport != null)
    {
      receiveActivity?.SetTags("remoteTransportContext", remoteTransport.TransportContext);
    }
    receiveActivity?.SetTag("objectId", objectId);

    try
    {
      using IDisposable? d1 = UseDefaultTransportIfNull(localTransport, out localTransport);
      receiveActivity?.SetTags("localTransportContext", localTransport.TransportContext);

      var result = await ReceiveImpl(objectId, remoteTransport, localTransport, onProgressAction, cancellationToken)
        .ConfigureAwait(false);

      receiveActivity?.SetStatus(SdkActivityStatusCode.Ok);
      return result;
    }
    catch (Exception ex)
    {
      receiveActivity?.SetStatus(SdkActivityStatusCode.Error);
      receiveActivity?.RecordException(ex);
      throw;
    }
  }

  /// <inheritdoc cref="Receive(string,ITransport?,ITransport?,IProgress{ProgressArgs}?,CancellationToken)"/>
  private async Task<Base> ReceiveImpl(
    string objectId,
    ITransport? remoteTransport,
    ITransport localTransport,
    IProgress<ProgressArgs>? internalProgressAction,
    CancellationToken cancellationToken
  )
  {
    // Setup Local Transport
    localTransport.OnProgressAction = internalProgressAction;
    localTransport.CancellationToken = cancellationToken;

    // Setup Remote Transport
    if (remoteTransport is not null)
    {
      remoteTransport.OnProgressAction = internalProgressAction;
      remoteTransport.CancellationToken = cancellationToken;
    }

    // Setup Serializer
    SpeckleObjectDeserializer serializer =
      new()
      {
        ReadTransport = localTransport,
        OnProgressAction = internalProgressAction,
        CancellationToken = cancellationToken,
        BlobStorageFolder = (remoteTransport as IBlobCapableTransport)?.BlobStorageFolder,
      };

    // Try Local Receive
    string? objString = await LocalReceive(objectId, localTransport).ConfigureAwait(false);

    if (objString is null)
    {
      // Fall back to remote
      if (remoteTransport is null)
      {
        throw new TransportException(
          $"Could not find specified object using the local transport {localTransport.TransportName}, and you didn't provide a fallback remote from which to pull it."
        );
      }

      logger.LogDebug(
        "Cannot find object {objectId} in the local transport, hitting remote {transportName}",
        objectId,
        remoteTransport.TransportName
      );

      objString = await RemoteReceive(objectId, remoteTransport, localTransport).ConfigureAwait(false);
    }

    using var serializerActivity = activityFactory.Start();

    // Proceed to deserialize the object, now safely knowing that all its children are present in the local (fast) transport.
    return await DeserializeActivity(objString, serializer).ConfigureAwait(false);
  }

  /// <summary>
  /// Try and get the object from the local transport. If it's there, we assume all its children are there
  /// This assumption is hard-wired into the <see cref="SpeckleObjectDeserializer"/>
  /// </summary>
  /// <param name="objectId"></param>
  /// <param name="localTransport"></param>
  /// <returns></returns>
  /// <exception cref="SpeckleDeserializeException"></exception>
  internal static async Task<string?> LocalReceive(string objectId, ITransport localTransport)
  {
    string? objString = await localTransport.GetObject(objectId).ConfigureAwait(false);
    if (objString is null)
    {
      return null;
    }
    return objString;
  }

  /// <summary>
  /// Copies the requested object and all its children from <paramref name="remoteTransport"/> to <paramref name="localTransport"/>
  /// </summary>
  /// <seealso cref="ITransport.CopyObjectAndChildren"/>
  /// <param name="objectId"></param>
  /// <param name="remoteTransport"></param>
  /// <param name="localTransport"></param>
  /// <returns></returns>
  /// <exception cref="TransportException">Remote transport was not specified</exception>
  private static async Task<string> RemoteReceive(
    string objectId,
    ITransport remoteTransport,
    ITransport localTransport
  )
  {
    var objString = await remoteTransport.CopyObjectAndChildren(objectId, localTransport).ConfigureAwait(false);

    // DON'T THINK THIS IS NEEDED CopyObjectAndChildren should call this
    // Wait for the local transport to finish "writing" - in this case, it signifies that the remote transport has done pushing copying objects into it. (TODO: I can see some scenarios where latency can screw things up, and we should rather wait on the remote transport).
    await localTransport.WriteComplete().ConfigureAwait(false);

    return objString;
  }

  private static IDisposable? UseDefaultTransportIfNull(ITransport? userTransport, out ITransport actualLocalTransport)
  {
    if (userTransport is not null)
    {
      actualLocalTransport = userTransport;
      return null;
    }

    //User did not specify a transport, default to SQLite
    SQLiteTransport defaultLocalTransport = new();
    actualLocalTransport = defaultLocalTransport;
    return defaultLocalTransport;
  }
}
