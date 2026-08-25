using Microsoft.Extensions.Logging;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Models;
using Speckle.Sdk.Pipelines.Receive.Artifacts;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Serialisation.V2.Receive;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Api;

public partial class Operations
{
  /// <summary>
  /// Receives the object graph behind <paramref name="objectId"/> from the server at <paramref name="url"/>.
  /// </summary>
  /// <remarks>
  /// <paramref name="objectId"/> is normally <c>Version.referencedObject</c>. Two forms are accepted transparently:
  /// <list type="bullet">
  /// <item>an object hash — the legacy JSON object graph is downloaded, cached and deserialized (unchanged behaviour);</item>
  /// <item>a <see cref="BundleReference"/> (<c>bundle.1.&lt;projectId&gt;.&lt;modelId&gt;.&lt;versionId&gt;</c>) — the
  /// version is bundle-only (Speckle 2026.9.0); its artefact bundle is downloaded via the <c>/api/v2</c> artifacts rail and
  /// materialized into a <see cref="Base"/> tree in the v3 DataObject idiom. The returned root carries
  /// <c>version = 4</c> so callers can tell a materialized tree from a genuine legacy one.</item>
  /// </list>
  /// Existing scripts therefore keep working after an SDK bump without code changes — but the bundle branch is a
  /// lossy compatibility projection (no typed classes, per-parameter metadata collapsed to scalars, synthetic ids for
  /// non-object entities) and it does not scale to the model sizes the bundle format is built for: every object,
  /// property and decoded mesh becomes a managed <see cref="Base"/>, so large versions that the new rail handles
  /// comfortably can exhaust memory here. New code should call <see cref="Receive3"/>, which returns the bundle itself.
  /// </remarks>
  /// <exception cref="ArgumentException">No transports were specified</exception>
  /// <exception cref="ArgumentNullException">The <paramref name="objectId"/> was <see langword="null"/></exception>
  /// <exception cref="SpeckleException">Serialization or Send operation was unsuccessful</exception>
  /// <exception cref="HttpRequestException">HTTP layer errors</exception>
  /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> requested cancellation</exception>
  [Obsolete(
    "Receive2 keeps working for existing versions, but for versions created with the new Speckle object model "
      + "(bundle-only, Speckle 2026.9.0) it returns a best-effort Base projection and does not guarantee the new object "
      + "model round-trips perfectly. It is also not suited to the model sizes the new format supports: materializing "
      + "a large bundle into a Base tree inflates every object, property and mesh into managed objects and can run out "
      + "of memory. Update your scripts to Receive3, which receives the bundle directly."
  )]
  public async Task<Base> Receive2(
    Uri url,
    string streamId,
    string objectId,
    string? authorizationToken,
    IProgress<ProgressArgs>? onProgressAction,
    CancellationToken cancellationToken,
    DeserializeProcessOptions? options = null
  )
  {
    // Speckle 2026.9.0 dispatch: a bundle-only version has no legacy object graph to download. Decide here, above
    // everything else, so the legacy path below stays byte-for-byte what it was.
    if (BundleReference.TryParse(objectId, out var bundleReference))
    {
      return await ReceiveBundle(url, streamId, bundleReference, authorizationToken, cancellationToken)
        .ConfigureAwait(false);
    }

    using var receiveActivity = activityFactory.Start("Operations.Receive");
    receiveActivity?.SetTag("speckle.url", url);
    receiveActivity?.SetTag("speckle.projectId", streamId);
    receiveActivity?.SetTag("speckle.objectId", objectId);
    metricsFactory.CreateCounter<long>("Receive").Add(1);

    var process = deserializeProcessFactory.CreateDeserializeProcess(
      url,
      streamId,
      authorizationToken,
      onProgressAction,
      cancellationToken,
      options
    );
    try
    {
      var result = await process.Deserialize(objectId).ConfigureAwait(false);
      receiveActivity?.SetStatus(SdkActivityStatusCode.Ok);
      return result;
    }
    catch (OperationCanceledException)
    {
      //this is handled by the caller
      throw;
    }
    catch (Exception ex)
    {
      receiveActivity?.SetStatus(SdkActivityStatusCode.Error);
      receiveActivity?.RecordException(ex);
      throw;
    }
    finally
    {
      await process.DisposeAsync().ConfigureAwait(false);
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
  /// <exception cref="NotSupportedException"><paramref name="objectId"/> is a <see cref="BundleReference"/>: transports
  /// serve per-object JSON and can never carry a Speckle 2026.9.0 bundle. Use <see cref="Receive3"/> instead.</exception>
  [Obsolete(
    "Transport-based Receive is frozen legacy surface: it works for existing (object-graph) versions but can never "
      + "receive versions created with the new Speckle object model (bundle-only, Speckle 2026.9.0) - a bundle "
      + "reference throws NotSupportedException. It is also the slowest path (per-object JSON, single-threaded "
      + "deserialization). Update your scripts to Receive3, which receives the bundle directly, or Receive2 if you "
      + "still need a Base tree."
  )]
  public async Task<Base> Receive(
    string objectId,
    ITransport? remoteTransport = null,
    ITransport? localTransport = null,
    IProgress<ProgressArgs>? onProgressAction = null,
    CancellationToken cancellationToken = default
  )
  {
    if (BundleReference.IsBundleReference(objectId))
    {
      throw new NotSupportedException(
        $"'{objectId}' is a bundle reference, not an object id: this version is bundle-only and has no legacy object "
          + "graph, so the transport-based Receive cannot serve it. Call Operations.Receive3(url, projectId, modelId, "
          + "versionId, token, ...) to receive the bundle, or Receive2(url, projectId, version.referencedObject, "
          + "token, ...) if you still need a Base tree."
      );
    }

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
    SpeckleObjectDeserializer serializer = new()
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
