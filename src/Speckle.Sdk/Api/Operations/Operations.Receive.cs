using Microsoft.Extensions.Logging;
using Speckle.InterfaceGenerator;
using Speckle.Objects.Utils;
using Speckle.Sdk.Bundles;
using Speckle.Sdk.Credentials;
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
  /// <see cref="Receive3(Uri, string, string, string, string?, ReceiveOptions?, CancellationToken)"/> addressed by a
  /// model url — <c>{server}/projects/{projectId}/models/{modelId}[@{versionId}]</c>, as the web app hands out. Without
  /// <c>@versionId</c> the model's latest version is resolved via GraphQL first.
  /// </summary>
  /// <exception cref="ArgumentException"><paramref name="modelUrl"/> is not a model url.</exception>
  /// <exception cref="SpeckleException">The model has no versions, or the version has no bundle.</exception>
  public async Task<Model> Receive3(
    string modelUrl,
    string? authorizationToken,
    ReceiveOptions? options,
    CancellationToken cancellationToken
  )
  {
    var url = ModelUrl.Parse(new Uri(modelUrl, UriKind.Absolute));
    string versionId =
      url.VersionId ?? await ResolveLatestVersionId(url, authorizationToken, cancellationToken).ConfigureAwait(false);
    return await Receive3(
        url.Server,
        url.ProjectId,
        url.ModelId,
        versionId,
        authorizationToken,
        options,
        cancellationToken
      )
      .ConfigureAwait(false);
  }

  private async Task<string> ResolveLatestVersionId(ModelUrl url, string? authorizationToken, CancellationToken ct)
  {
    var account = new Account
    {
      token = authorizationToken ?? string.Empty,
      serverInfo = new() { url = url.Server.ToString() },
      userInfo = new(),
    };
    using var client = clientFactory.Create(account);
    var model = await client
      .Model.GetWithVersions(url.ModelId, url.ProjectId, versionsLimit: 1, cancellationToken: ct)
      .ConfigureAwait(false);
    if (model.versions.items.Count == 0)
    {
      throw new SpeckleException($"Model '{url.ModelId}' in project '{url.ProjectId}' has no versions yet.");
    }
    return model.versions.items[0].id;
  }

  /// <summary>
  /// Receives a version of a model — the Speckle 2026.9.0 read path. Addresses the version by id, downloads its
  /// artefact bundle via the <c>/api/v2</c> artifacts rail and returns a <see cref="Model"/>: objects with their
  /// instance- and type-level properties, ready to query with LINQ, plus the raw bundle graph underneath. Nothing is
  /// projected or dropped.
  /// </summary>
  /// <remarks>
  /// Works for every version the server has a bundle for — natively published ones and legacy versions the server
  /// has converted. The returned <see cref="Model"/> keeps the bundle files on disk until it is disposed, so wrap it
  /// in <c>using</c>.
  /// <para><b>Memory.</b> Objects, properties, nodes and relations are parsed fully into memory up front; geometry is
  /// parsed only when <see cref="Model.Geometries"/> is first accessed (and not downloaded at all with
  /// <see cref="ReceiveOptions.IncludeGeometry"/> false). This is the in-process path: it suits models whose
  /// property tables fit comfortably in RAM. For very large models — or for questions that only touch a slice of
  /// the data — use the SQL query API (the separate <c>Speckle.Sdk.Query</c> package), which streams over the same
  /// parquet files on disk instead of loading them.</para>
  /// </remarks>
  /// <param name="options">What to download; <see langword="null"/> = <see cref="ReceiveOptions.Default"/> (viewer
  /// artefacts skipped).</param>
  /// <exception cref="SpeckleException">The server returned no artefact files for this version (no bundle exists yet,
  /// the server predates the /api/v2 artifacts endpoint, or the token cannot read the project).</exception>
  /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> requested cancellation</exception>
  public async Task<Model> Receive3(
    Uri url,
    string projectId,
    string modelId,
    string versionId,
    string? authorizationToken,
    ReceiveOptions? options,
    CancellationToken cancellationToken
  )
  {
    options ??= ReceiveOptions.Default;
    using var receiveActivity = activityFactory.Start("Operations.Receive3");
    receiveActivity?.SetTag("speckle.url", url);
    receiveActivity?.SetTag("speckle.projectId", projectId);
    receiveActivity?.SetTag("speckle.modelId", modelId);
    receiveActivity?.SetTag("speckle.versionId", versionId);
    metricsFactory.CreateCounter<long>("Receive").Add(1);

    try
    {
      var model = await DownloadBundleModel(
          url,
          projectId,
          modelId,
          versionId,
          authorizationToken,
          options,
          cancellationToken
        )
        .ConfigureAwait(false);
      receiveActivity?.SetStatus(SdkActivityStatusCode.Ok);
      return model;
    }
    catch (OperationCanceledException)
    {
      throw;
    }
    catch (Exception ex)
    {
      receiveActivity?.SetStatus(SdkActivityStatusCode.Error);
      receiveActivity?.RecordException(ex);
      throw;
    }
  }

  /// <summary>
  /// Receives the object graph behind <paramref name="objectId"/> from the server at <paramref name="url"/>.
  /// </summary>
  /// <remarks>
  /// <paramref name="objectId"/> is normally <c>Version.referencedObject</c>. Two forms are accepted transparently:
  /// <list type="bullet">
  /// <item>an object hash — the legacy JSON object graph is downloaded, cached and deserialized (unchanged behaviour);</item>
  /// <item>a <see cref="BundleReference"/> (<c>bundle.&lt;projectId&gt;.&lt;modelId&gt;.&lt;versionId&gt;</c>) — the
  /// version is bundle-only (Speckle 2026.9.0); its artefact bundle is downloaded via the <c>/api/v2</c> artifacts rail and
  /// materialized into a <see cref="Base"/> tree in the v3 DataObject idiom. The returned root carries
  /// <c>version = 4</c> so callers can tell a materialized tree from a genuine legacy one.</item>
  /// </list>
  /// Existing scripts therefore keep working after an SDK bump without code changes — but the bundle branch is a
  /// lossy compatibility projection (no typed classes, per-parameter metadata collapsed to scalars, synthetic ids for
  /// non-object entities) and it does not scale to the model sizes the bundle format is built for: every object,
  /// property and decoded mesh becomes a managed <see cref="Base"/>, so large versions that the new rail handles
  /// comfortably can exhaust memory here. New code should call <see cref="Receive3(Uri, string, string, string, string?, ReceiveOptions?, CancellationToken)"/>, which returns the bundle itself.
  /// </remarks>
  /// <exception cref="ArgumentException">No transports were specified</exception>
  /// <exception cref="ArgumentNullException">The <paramref name="objectId"/> was <see langword="null"/></exception>
  /// <exception cref="SpeckleException">Serialization or Send operation was unsuccessful</exception>
  /// <exception cref="HttpRequestException">HTTP layer errors</exception>
  /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> requested cancellation</exception>
  [AutoInterfaceIgnore] // declared by hand on IOperations so the [Obsolete] reaches interface callers
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
  /// serve per-object JSON and can never carry a Speckle 2026.9.0 bundle. Use <see cref="Receive3(Uri, string, string, string, string?, ReceiveOptions?, CancellationToken)"/> instead.</exception>
  [AutoInterfaceIgnore] // declared by hand on IOperations so the [Obsolete] reaches interface callers
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

  /// <summary>
  /// The Speckle 2026.9.0 rail behind <see cref="Receive2"/>: downloads the version's artefact bundle via
  /// <c>/api/v2/.../artifacts</c> and materializes it into a <see cref="Base"/> tree that legacy traversal code can
  /// walk. The shape follows the receive fidelity contract (atlas spec <c>2026-08-big-truck-dev-compat</c> §6):
  /// <list type="bullet">
  /// <item>root is a <c>Collection</c> carrying <c>units</c>, <c>version = 4</c>, and the render-material /
  /// instance-definition proxy lists;</item>
  /// <item>objects are <c>DataObject</c>s keyed by <c>applicationId</c> (never a content hash), with SGEO-decoded
  /// <c>displayValue</c> meshes and a nested <c>properties</c> dict rebuilt from EAV paths;</item>
  /// <item>collections / materials / definitions get per-bundle synthetic ids — stable within one tree, not across versions;</item>
  /// <item>v2 typed classes are NOT rehydrated; per-parameter metadata collapses to scalars.</item>
  /// </list>
  /// Full fidelity lives in <see cref="Receive3(Uri, string, string, string, string?, ReceiveOptions?, CancellationToken)"/>.
  /// </summary>
  /// <exception cref="SpeckleException">The reference's project doesn't match <paramref name="projectId"/>, or the
  /// server returned no artefact files for a version that promises a bundle.</exception>
  private async Task<Base> ReceiveBundle(
    Uri url,
    string projectId,
    BundleReference reference,
    string? authorizationToken,
    CancellationToken cancellationToken
  )
  {
    using var receiveActivity = activityFactory.Start("Operations.Receive.Bundle");
    receiveActivity?.SetTag("speckle.url", url);
    receiveActivity?.SetTag("speckle.projectId", projectId);
    receiveActivity?.SetTag("speckle.bundleReference", reference.ToString());
    metricsFactory.CreateCounter<long>("Receive").Add(1);

    try
    {
      if (reference.ProjectId != projectId)
      {
        throw new SpeckleException(
          $"Bundle reference '{reference}' belongs to project '{reference.ProjectId}', but the receive was requested "
            + $"for project '{projectId}'."
        );
      }

      using var model = await DownloadBundleModel(
          url,
          reference.ProjectId,
          reference.ModelId,
          reference.VersionId,
          authorizationToken,
          ReceiveOptions.Default,
          cancellationToken
        )
        .ConfigureAwait(false);

      // The Base projection walks nested property dictionaries and decodes every mesh: re-read the downloaded files
      // in the eager profile. This is the lossy compat path; its memory cost is the reason Receive2 is obsolete.
      var eager = await ArtefactBundleReader
        .ReadAsync(model.Directory, ArtefactReadOptions.Eager, cancellationToken)
        .ConfigureAwait(false);
      var root = new ObjectsArtifactReader().Build(
        eager,
        new ArtifactReceiveOptions(PreferSolids: false),
        cancellationToken
      );

      // Vintage marker: lets consumers (and the migrator's IsV3 check) tell a materialized tree from a genuine
      // v2/v3 graph. Same convention as BundleMigrator's TreeMaterializer.
      root["version"] = 4;

      receiveActivity?.SetStatus(SdkActivityStatusCode.Ok);
      return root;
    }
    catch (OperationCanceledException)
    {
      throw;
    }
    catch (Exception ex)
    {
      receiveActivity?.SetStatus(SdkActivityStatusCode.Error);
      receiveActivity?.RecordException(ex);
      throw;
    }
  }

  /// <summary>Downloads the bundle into a scratch directory and parses it. The returned <see cref="Model"/> owns the
  /// directory; on any failure the directory is removed here.</summary>
  private async Task<Model> DownloadBundleModel(
    Uri url,
    string projectId,
    string modelId,
    string versionId,
    string? authorizationToken,
    ReceiveOptions options,
    CancellationToken cancellationToken
  )
  {
    // ArtifactDownloader only reads token + serverInfo.url off the account.
    var account = new Account
    {
      token = authorizationToken ?? string.Empty,
      serverInfo = new() { url = url.ToString() },
      userInfo = new(),
    };

    string bundleDir = Path.Combine(
      SpecklePathProvider.UserApplicationDataPath(),
      "Speckle",
      "BundleReceive",
      Guid.NewGuid().ToString("N")
    );
    try
    {
      var files = await artifactDownloader
        .DownloadBundleAsync(
          account,
          projectId,
          modelId,
          versionId,
          bundleDir,
          options.ShouldDownload,
          cancellationToken
        )
        .ConfigureAwait(false);

      if (files.Count == 0)
      {
        // Never fall back to the legacy object path from here: a caller on this rail asked for the bundle, and for
        // a bundle-only version there is no legacy graph anyway.
        throw new SpeckleException(
          $"Version '{versionId}' (model '{modelId}', project '{projectId}') has no artefact bundle on the server at "
            + $"'{url}'. Either the server does not serve the /api/v2 artifacts endpoint yet, the token cannot read the "
            + "project, or the bundle has not been produced for this version."
        );
      }

      // Geometry stays on disk until Model.Geometries is touched — it is the bulk of every bundle — and properties
      // stay columnar (PropertyTable) so memory tracks the parquet size, not a dictionary per nesting level.
      var bundle = await ArtefactBundleReader
        .ReadAsync(bundleDir, ArtefactReadOptions.Columnar, cancellationToken)
        .ConfigureAwait(false);
      return new Model(projectId, modelId, versionId, bundleDir, files, bundle, options.IncludeGeometry, logger);
    }
    catch
    {
      TryDeleteDirectory(bundleDir);
      throw;
    }
  }

  private void TryDeleteDirectory(string dir)
  {
    try
    {
      if (Directory.Exists(dir))
      {
        Directory.Delete(dir, true);
      }
    }
    catch (IOException ex)
    {
      logger.LogWarning(ex, "Could not clean up bundle scratch directory {bundleDir}", dir);
    }
    catch (UnauthorizedAccessException ex)
    {
      logger.LogWarning(ex, "Could not clean up bundle scratch directory {bundleDir}", dir);
    }
  }
}

/// <summary>Hand-declared members of the generated <see cref="IOperations"/>: the source generator doesn't copy
/// attributes, and the deprecation must reach callers that resolve <c>IOperations</c> from DI (every connector and
/// script), not only those holding a concrete <see cref="Operations"/>.</summary>
public partial interface IOperations
{
  /// <inheritdoc cref="Operations.Receive2"/>
  [Obsolete(
    "Receive2 keeps working for existing versions, but for versions created with the new Speckle object model "
      + "(bundle-only, Speckle 2026.9.0) it returns a best-effort Base projection and does not guarantee the new object "
      + "model round-trips perfectly. It is also not suited to the model sizes the new format supports: materializing "
      + "a large bundle into a Base tree inflates every object, property and mesh into managed objects and can run out "
      + "of memory. Update your scripts to Receive3, which receives the bundle directly."
  )]
  Task<Base> Receive2(
    Uri url,
    string streamId,
    string objectId,
    string? authorizationToken,
    IProgress<ProgressArgs>? onProgressAction,
    CancellationToken cancellationToken,
    DeserializeProcessOptions? options = null
  );

  /// <inheritdoc cref="Operations.Receive"/>
  [Obsolete(
    "Transport-based Receive is frozen legacy surface: it works for existing (object-graph) versions but can never "
      + "receive versions created with the new Speckle object model (bundle-only, Speckle 2026.9.0) - a bundle "
      + "reference throws NotSupportedException. It is also the slowest path (per-object JSON, single-threaded "
      + "deserialization). Update your scripts to Receive3, which receives the bundle directly, or Receive2 if you "
      + "still need a Base tree."
  )]
  Task<Base> Receive(
    string objectId,
    ITransport? remoteTransport = null,
    ITransport? localTransport = null,
    IProgress<ProgressArgs>? onProgressAction = null,
    CancellationToken cancellationToken = default
  );
}
