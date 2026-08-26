using Microsoft.Extensions.Logging;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Pipelines.Receive.Artifacts;
using Speckle.Sdk.Pipelines.Send.Artifacts;

namespace Speckle.Sdk.Bundles;

/// <summary>
/// Publishes a <see cref="BundleBuilder"/> as a new version: creates the ingestion (the server pre-allocates the
/// version id), finishes the builder and re-keys its files to that id, uploads them over the <c>/api/v2</c> artifacts
/// rail (sign → presigned PUT per file → complete), and marks the ingestion failed or cancelled on any error. The
/// orchestration behind <see cref="Operations.Send3(Uri, string, string, BundleBuilder, string?, SendOptions?, CancellationToken)"/>;
/// connectors can drive it directly once their host objects are in a builder.
/// </summary>
[GenerateAutoInterface]
public sealed class BundleSender(
  IClientFactory clientFactory,
  IArtifactPipelineFactory artifactPipelineFactory,
  ISdkActivityFactory activityFactory,
  ILogger<BundleSender> logger
) : IBundleSender
{
  /// <exception cref="SpeckleException">The server did not pre-allocate a version id (it predates the v2 data endpoints).</exception>
  public async Task<SendResult> SendAsync(
    Uri url,
    string projectId,
    string modelId,
    BundleBuilder builder,
    string? authorizationToken,
    SendOptions options,
    CancellationToken cancellationToken
  )
  {
    using var activity = activityFactory.Start("BundleSender.Send");
    activity?.SetTag("speckle.url", url);
    activity?.SetTag("speckle.projectId", projectId);
    activity?.SetTag("speckle.modelId", modelId);

    var account = new Account
    {
      token = authorizationToken ?? string.Empty,
      serverInfo = new() { url = url.ToString() },
      userInfo = new(),
    };
    using var client = clientFactory.Create(account);

    var ingestion = await client
      .Ingestion.Create(
        new(
          modelId,
          projectId,
          options.Message ?? $"Sending from {builder.Producer.ApplicationAndVersion}",
          new(builder.Producer.Slug, builder.Producer.HostApplicationVersion, options.FileName, options.FileSizeBytes),
          options.MaxIdleTimeoutSeconds
        ),
        cancellationToken
      )
      .ConfigureAwait(false);
    activity?.SetTag("speckle.ingestionId", ingestion.id);

    if (ingestion.versionId is not { Length: > 0 } versionId)
    {
      throw new SpeckleException(
        $"The server at '{url}' did not pre-allocate a version id for the ingestion; the Speckle 2026.9.0 bundle "
          + "upload requires a server with the /api/v2 data endpoints."
      );
    }

    try
    {
      // Build under the temporary basename, then re-key the files to the version id the server just allocated:
      // the v2 upload signs and keys every file by its basename.
      BundleFiles files = builder.Build().RenameTo(versionId);
      string rootId = new BundleReference(projectId, modelId, versionId).ToString();

      using var pipeline = artifactPipelineFactory.CreateInstance(
        projectId,
        ingestion.id,
        versionId,
        account,
        files.Directory,
        cancellationToken
      );
      string committedVersionId = await pipeline
        .UploadFilesAsync(files.ByName, rootId, files.ObjectCount)
        .ConfigureAwait(false);

      if (!options.KeepFiles)
      {
        TryDeleteDirectory(files.Directory);
      }
      activity?.SetStatus(SdkActivityStatusCode.Ok);
      return new SendResult(projectId, modelId, committedVersionId, ingestion.id, files.ObjectCount);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      await client
        .Ingestion.FailWithCancel(new(ingestion.id, projectId, "Cancelled by the caller"), CancellationToken.None)
        .ConfigureAwait(false);
      throw;
    }
    catch (Exception ex)
    {
      activity?.SetStatus(SdkActivityStatusCode.Error);
      activity?.RecordException(ex);
      await client
        .Ingestion.FailWithError(
          ModelIngestionFailedInput.FromException(ingestion.id, projectId, ex),
          CancellationToken.None
        )
        .ConfigureAwait(false);
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
      logger.LogWarning(ex, "Could not clean up bundle directory {dir}", dir);
    }
    catch (UnauthorizedAccessException ex)
    {
      logger.LogWarning(ex, "Could not clean up bundle directory {dir}", dir);
    }
  }
}
