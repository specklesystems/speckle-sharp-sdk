using Microsoft.Extensions.Logging;
using Speckle.InterfaceGenerator;
using Speckle.Objects.Utils;
using Speckle.Sdk.Api;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Models;
using Speckle.Sdk.Pipelines.Receive.Artifacts;

namespace Speckle.Sdk.Bundles;

/// <summary>
/// Fetches a version's artefact bundle over the <c>/api/v2</c> artifacts rail and parses it — the orchestration behind
/// <see cref="Operations.Receive3(Account, string, string, string, ReceiveOptions?, CancellationToken)"/>.
/// The <see cref="Model"/> profile (columnar properties, deferred geometry) is the read surface; the <see cref="Base"/>
/// profile is the lossy projection <see cref="Operations.Receive2"/> hands legacy scripts.
/// </summary>
[GenerateAutoInterface]
public sealed class BundleReceiver(
  IArtifactDownloader artifactDownloader,
  IClientFactory clientFactory,
  IVersionReceivedMarker receivedMarker,
  ILogger<BundleReceiver> logger
) : IBundleReceiver
{
  /// <summary>Downloads the bundle into a scratch directory and parses it in the columnar profile. The returned
  /// <see cref="Model"/> owns the directory; on any failure the directory is removed here.</summary>
  /// <exception cref="SpeckleException">The server returned no artefact files for this version.</exception>
  public async Task<Model> ReceiveAsync(
    Account account,
    string projectId,
    string modelId,
    string versionId,
    ReceiveOptions options,
    CancellationToken cancellationToken
  )
  {
    string bundleDir = await DownloadAsync(account, projectId, modelId, versionId, options, cancellationToken)
      .ConfigureAwait(false);
    try
    {
      // Geometry stays on disk until Model.Geometries is touched — it is the bulk of every bundle — and properties
      // stay columnar (PropertyTable) so memory tracks the parquet size, not a dictionary per nesting level.
      var bundle = await ArtefactBundleReader
        .ReadAsync(bundleDir, ArtefactReadOptions.Columnar, cancellationToken)
        .ConfigureAwait(false);
      var files = Directory.EnumerateFiles(bundleDir).OrderBy(p => p, StringComparer.Ordinal).ToList();
      var model = new Model(projectId, modelId, versionId, bundleDir, files, bundle, options.IncludeGeometry, logger);
      if (options.MarkReceived)
      {
        await receivedMarker.MarkAsync(account, projectId, versionId, cancellationToken).ConfigureAwait(false);
      }
      return model;
    }
    catch
    {
      TryDeleteDirectory(bundleDir);
      throw;
    }
  }

  /// <summary>
  /// The legacy projection: downloads the bundle and materializes it into a <see cref="Base"/> tree in the v3
  /// DataObject idiom (atlas spec <c>2026-08-big-truck-dev-compat</c> §6) — a <c>Collection</c> root carrying
  /// <c>units</c>, <c>version = 4</c> and the proxy lists; <c>DataObject</c>s keyed by <c>applicationId</c> with
  /// SGEO-decoded <c>displayValue</c> and nested <c>properties</c>; synthetic per-bundle ids for non-object entities;
  /// no typed-class rehydration. Reads the eager profile (every property nested, every mesh decoded) — the memory
  /// cost that makes <see cref="Operations.Receive2"/> obsolete. The scratch files are deleted before returning.
  /// </summary>
  public async Task<Base> ReceiveAsBaseAsync(
    Account account,
    string projectId,
    string modelId,
    string versionId,
    CancellationToken cancellationToken
  )
  {
    string bundleDir = await DownloadAsync(
        account,
        projectId,
        modelId,
        versionId,
        ReceiveOptions.Default,
        cancellationToken
      )
      .ConfigureAwait(false);
    try
    {
      var eager = await ArtefactBundleReader
        .ReadAsync(bundleDir, ArtefactReadOptions.Eager, cancellationToken)
        .ConfigureAwait(false);
      var root = new ObjectsArtifactReader().Build(
        eager,
        new ArtifactReceiveOptions(PreferSolids: false),
        cancellationToken
      );
      // Vintage marker: lets consumers (and the migrator's IsV3 check) tell a materialized tree from a genuine
      // v2/v3 graph. Same convention as BundleMigrator's TreeMaterializer.
      root["version"] = 4;
      await receivedMarker.MarkAsync(account, projectId, versionId, cancellationToken).ConfigureAwait(false);
      return root;
    }
    finally
    {
      TryDeleteDirectory(bundleDir);
    }
  }

  /// <summary>The model's latest version id, for a url without <c>@versionId</c>.</summary>
  /// <exception cref="SpeckleException">The model has no versions.</exception>
  public async Task<string> ResolveLatestVersionIdAsync(
    Account account,
    string projectId,
    string modelId,
    CancellationToken ct
  )
  {
    using var client = clientFactory.Create(account);
    var model = await client
      .Model.GetWithVersions(modelId, projectId, versionsLimit: 1, cancellationToken: ct)
      .ConfigureAwait(false);
    if (model.versions.items.Count == 0)
    {
      throw new SpeckleException($"Model '{modelId}' in project '{projectId}' has no versions yet.");
    }
    return model.versions.items[0].id;
  }

  private async Task<string> DownloadAsync(
    Account account,
    string projectId,
    string modelId,
    string versionId,
    ReceiveOptions options,
    CancellationToken cancellationToken
  )
  {
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
            + $"'{account.serverInfo.url}'. Either the server does not serve the /api/v2 artifacts endpoint yet, the token cannot read the "
            + "project, or the bundle has not been produced for this version."
        );
      }
      return bundleDir;
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
