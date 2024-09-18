using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Serialisation.Receive;
using Speckle.Sdk.Serialisation.Utilities;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Api;

public partial class Operations
{
  public async ValueTask<Base> Receive2(
    Account account,
    string projectId,
    string objectId,
    Action<ProgressArgs[]>? onProgressAction = null,
    CancellationToken cancellationToken = default
  )
  {
#pragma warning disable CA2000
    using var stage = new ReceiveProcess(
      new ServerSource(speckleHttp, activityFactory, new Uri(account.serverInfo.url), projectId, null)
    );
#pragma warning restore CA2000
    var rootObject = await stage.GetObject(objectId, onProgressAction, cancellationToken).ConfigureAwait(false);
    return rootObject;
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
  /// <param name="onTotalChildrenCountKnown">Action invoked once the total count of objects is known</param>
  /// <param name="cancellationToken"></param>
  /// <exception cref="TransportException">Failed to retrieve objects from the provided transport(s)</exception>
  /// <exception cref="SpeckleDeserializeException">Deserialization of the requested object(s) failed</exception>
  /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> requested cancel</exception>
  /// <returns>The requested Speckle Object</returns>
  public async Task<Base> Receive(
    string objectId,
    ITransport? remoteTransport = null,
    ITransport? localTransport = null,
    Action<ConcurrentBag<ProgressArgs>>? onProgressAction = null,
    Action<int>? onTotalChildrenCountKnown = null,
    CancellationToken cancellationToken = default
  )
  {
    // Setup Progress Reporting
    var internalProgressAction = GetInternalProgressAction(onProgressAction);

    // Setup Local Transport
    using IDisposable? d1 = UseDefaultTransportIfNull(localTransport, out localTransport);
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
        BlobStorageFolder = (remoteTransport as IBlobCapableTransport)?.BlobStorageFolder
      };

    // Setup Logging
    using var receiveActivity = activityFactory.Start();
    receiveActivity?.SetTag("remoteTransportContext", remoteTransport?.TransportContext);
    receiveActivity?.SetTag("localTransportContext", localTransport.TransportContext);
    receiveActivity?.SetTag("objectId", objectId);
    var timer = Stopwatch.StartNew();

    // Receive Json
    logger.LogDebug(
      "Starting receive {objectId} from transports {localTransport} / {remoteTransport}",
      objectId,
      localTransport.TransportName,
      remoteTransport?.TransportName
    );

    // Try Local Receive
    string? objString = await LocalReceive(objectId, localTransport, onTotalChildrenCountKnown).ConfigureAwait(false);

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

      objString = await RemoteReceive(objectId, remoteTransport, localTransport, onTotalChildrenCountKnown)
        .ConfigureAwait(false);
    }

    using var activity = activityFactory.Start("Deserialize");
    // Proceed to deserialize the object, now safely knowing that all its children are present in the local (fast) transport.
    Base res = await serializer.DeserializeAsync(objString).ConfigureAwait(false);

    timer.Stop();
    logger.LogDebug(
      "Finished receiving {objectId} from {source} in {elapsed} seconds",
      objectId,
      remoteTransport?.TransportName,
      timer.Elapsed.TotalSeconds
    );

    return res;
  }

  /// <summary>
  /// Try and get the object from the local transport. If it's there, we assume all its children are there
  /// This assumption is hard-wired into the <see cref="SpeckleObjectDeserializer"/>
  /// </summary>
  /// <param name="objectId"></param>
  /// <param name="localTransport"></param>
  /// <param name="onTotalChildrenCountKnown"></param>
  /// <returns></returns>
  /// <exception cref="SpeckleDeserializeException"></exception>
  internal static async Task<string?> LocalReceive(
    string objectId,
    ITransport localTransport,
    Action<int>? onTotalChildrenCountKnown
  )
  {
    string? objString = await localTransport.GetObject(objectId).ConfigureAwait(false);
    if (objString is null)
    {
      return null;
    }

    // Shoot out the total children count, wasteful
    var count = ClosureParser.GetClosures(objString).Count;

    onTotalChildrenCountKnown?.Invoke(count);

    return objString;
  }

  /// <summary>
  /// Copies the requested object and all its children from <paramref name="remoteTransport"/> to <paramref name="localTransport"/>
  /// </summary>
  /// <seealso cref="ITransport.CopyObjectAndChildren"/>
  /// <param name="objectId"></param>
  /// <param name="remoteTransport"></param>
  /// <param name="localTransport"></param>
  /// <param name="onTotalChildrenCountKnown"></param>
  /// <returns></returns>
  /// <exception cref="TransportException">Remote transport was not specified</exception>
  private static async Task<string> RemoteReceive(
    string objectId,
    ITransport remoteTransport,
    ITransport localTransport,
    Action<int>? onTotalChildrenCountKnown
  )
  {
    var objString = await remoteTransport
      .CopyObjectAndChildren(objectId, localTransport, onTotalChildrenCountKnown)
      .ConfigureAwait(false);

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
