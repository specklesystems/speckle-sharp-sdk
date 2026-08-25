using Microsoft.Extensions.Logging;
using Speckle.Objects.Utils;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Models;
using Speckle.Sdk.Pipelines.Receive.Artifacts;

namespace Speckle.Sdk.Api;

public partial class Operations
{
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
  /// Full fidelity lives in the <c>/v2</c> artifacts rail and the local query API.
  /// </summary>
  /// <exception cref="SpeckleException">The reference's project doesn't match <paramref name="streamId"/>, or the
  /// server returned no artefact files for a version that promises a bundle.</exception>
  private async Task<Base> ReceiveBundle(
    Uri url,
    string streamId,
    BundleReference reference,
    string? authorizationToken,
    CancellationToken cancellationToken
  )
  {
    using var receiveActivity = activityFactory.Start("Operations.Receive.Bundle");
    receiveActivity?.SetTag("speckle.url", url);
    receiveActivity?.SetTag("speckle.projectId", streamId);
    receiveActivity?.SetTag("speckle.bundleReference", reference.ToString());
    metricsFactory.CreateCounter<long>("Receive").Add(1);

    try
    {
      if (reference.ProjectId != streamId)
      {
        throw new SpeckleException(
          $"Bundle reference '{reference}' belongs to project '{reference.ProjectId}', but the receive was requested "
            + $"for project '{streamId}'."
        );
      }

      var bundle = await DownloadAndReadBundle(
          url,
          reference.ProjectId,
          reference.ModelId,
          reference.VersionId,
          authorizationToken,
          cancellationToken
        )
        .ConfigureAwait(false);

      var root = new ObjectsArtifactReader().Build(
        bundle,
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

  /// <summary>
  /// Receives a version as its Speckle 2026.9.0 artefact bundle — the full-fidelity read path. Addresses the version by
  /// id (not by <c>referencedObject</c>), downloads its bundle via the <c>/api/v2</c> artifacts rail and parses it
  /// into an in-memory <see cref="ArtefactBundle"/>: dense-int object / geometry / node graph, EAV properties (instance
  /// and type-level), raw geometry blobs, relations, scene views. Nothing is projected or dropped.
  /// </summary>
  /// <remarks>
  /// Works for every version the server has a bundle for — natively published ones and legacy versions the server
  /// has converted. Successor to <see cref="Receive2"/>: that method returns a lossy <see cref="Base"/> projection
  /// for bundle-only versions; this one returns the bundle. To get a <see cref="Base"/> tree from the result,
  /// run it through <see cref="ObjectsArtifactReader.Build"/>.
  /// </remarks>
  /// <exception cref="SpeckleException">The server returned no artefact files for this version (no bundle exists yet,
  /// the server predates the /api/v2 artifacts endpoint, or the token cannot read the project).</exception>
  /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> requested cancellation</exception>
  public async Task<ArtefactBundle> Receive3(
    Uri url,
    string projectId,
    string modelId,
    string versionId,
    string? authorizationToken,
    CancellationToken cancellationToken
  )
  {
    using var receiveActivity = activityFactory.Start("Operations.Receive3");
    receiveActivity?.SetTag("speckle.url", url);
    receiveActivity?.SetTag("speckle.projectId", projectId);
    receiveActivity?.SetTag("speckle.modelId", modelId);
    receiveActivity?.SetTag("speckle.versionId", versionId);
    metricsFactory.CreateCounter<long>("Receive").Add(1);

    try
    {
      var bundle = await DownloadAndReadBundle(
          url,
          projectId,
          modelId,
          versionId,
          authorizationToken,
          cancellationToken
        )
        .ConfigureAwait(false);
      receiveActivity?.SetStatus(SdkActivityStatusCode.Ok);
      return bundle;
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

  /// <summary>Downloads the bundle into a scratch directory, parses it fully into memory, and removes the scratch.</summary>
  private async Task<ArtefactBundle> DownloadAndReadBundle(
    Uri url,
    string projectId,
    string modelId,
    string versionId,
    string? authorizationToken,
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
        .DownloadBundleAsync(account, projectId, modelId, versionId, bundleDir, cancellationToken)
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

      return await ArtefactBundleReader.ReadAsync(bundleDir, cancellationToken).ConfigureAwait(false);
    }
    finally
    {
      try
      {
        if (Directory.Exists(bundleDir))
        {
          Directory.Delete(bundleDir, true);
        }
      }
      catch (IOException ex)
      {
        logger.LogWarning(ex, "Could not clean up bundle scratch directory {bundleDir}", bundleDir);
      }
      catch (UnauthorizedAccessException ex)
      {
        logger.LogWarning(ex, "Could not clean up bundle scratch directory {bundleDir}", bundleDir);
      }
    }
  }
}
