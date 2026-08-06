// SPIKE(wayfinder ticket 07) — THROWAWAY. The artefact-bundle rail behind Receive2's crafted-id dispatch.
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Models;
using Speckle.Sdk.Pipelines.Receive.Artifacts;

namespace Speckle.Sdk.Api;

public partial class Operations
{
  private async Task<Base> ReceiveViaArtifactsSpike(
    Uri url,
    string streamId,
    CraftedResourceId craftedId,
    string? authorizationToken,
    CancellationToken cancellationToken
  )
  {
    using var receiveActivity = activityFactory.Start("Operations.Receive.Artifacts");
    receiveActivity?.SetTag("speckle.url", url);
    receiveActivity?.SetTag("speckle.projectId", streamId);
    receiveActivity?.SetTag("speckle.resourceId", craftedId.ToString());
    metricsFactory.CreateCounter<long>("Receive").Add(1);
    try
    {
      if (craftedId.ProjectId != streamId)
      {
        throw new SpeckleException(
          $"Resource id project '{craftedId.ProjectId}' does not match the requested project '{streamId}'."
        );
      }
      if (artifactDownloader is null || artifactGraphMaterializer is null)
      {
        throw new SpeckleException(
          "This version is bundle-only and no artefact materializer is available. Reference Speckle.Objects "
            + "and register IArtifactGraphMaterializer (or build Operations via AddSpeckleSdk DI)."
        );
      }

      // ArtifactDownloader wants an Account but only reads token + serverInfo.url (research 03 §4).
      var account = new Account
      {
        token = authorizationToken ?? "",
        serverInfo = new() { url = url.ToString() },
      };
      string bundleDir = Path.Combine(Path.GetTempPath(), "SpeckleSpike07", Guid.NewGuid().ToString("N"));
      try
      {
        var files = await artifactDownloader
          .DownloadBundleAsync(account, craftedId.ProjectId, craftedId.ModelId, craftedId.VersionId, bundleDir, cancellationToken)
          .ConfigureAwait(false);
        if (files.Count == 0)
        {
          // The readiness question (ticket 09): a crafted id promises a bundle; an empty listing means the
          // promise is broken (still building, or migration incomplete). Fail loud rather than fall back.
          throw new SpeckleException(
            $"Version '{craftedId.VersionId}' carries a bundle resource id but the server returned no artefact "
              + "files — the bundle is not (yet) available."
          );
        }
        var result = await artifactGraphMaterializer.MaterializeAsync(bundleDir, cancellationToken).ConfigureAwait(false);
        receiveActivity?.SetStatus(SdkActivityStatusCode.Ok);
        return result;
      }
      finally
      {
        if (Directory.Exists(bundleDir))
        {
          Directory.Delete(bundleDir, true);
        }
      }
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
}
