using Speckle.Sdk.Pipelines.Progress;

namespace Speckle.Sdk.BundleMigrator.Transports;

internal static class DuckDbHelper
{
  public static Task DownloadDuckFile(
    HttpClient client,
    string projectId,
    string modelId,
    string versionId,
    FileInfo destination,
    IProgress<StreamProgressArgs> downloadProgress,
    CancellationToken cancellationToken
  )
  {
    Uri url = new($"/api/v1/projects/{projectId}/models/{modelId}/versions/{versionId}/download", UriKind.Relative);
    return DownloadToFile(client, url, destination, downloadProgress, cancellationToken);
  }

  /// <summary>Downloads a packfile from a presigned url straight out of object storage. The url carries its
  /// own auth, so <paramref name="client"/> must be auth-free.</summary>
  public static Task DownloadFromUrl(
    HttpClient client,
    Uri presignedUrl,
    FileInfo destination,
    IProgress<StreamProgressArgs> downloadProgress,
    CancellationToken cancellationToken
  ) => DownloadToFile(client, presignedUrl, destination, downloadProgress, cancellationToken);

  private static async Task DownloadToFile(
    HttpClient client,
    Uri url,
    FileInfo destination,
    IProgress<StreamProgressArgs> downloadProgress,
    CancellationToken cancellationToken
  )
  {
    using var response = await client
      .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
      .ConfigureAwait(false);
    response.EnsureSuccessStatusCode();

    using var destinationStream = new FileStream(
      destination.FullName,
      FileMode.Create,
      FileAccess.Write,
      FileShare.None,
      1024 * 1024,
      FileOptions.Asynchronous
    );
    using ProgressStream progressStream = new(destinationStream, downloadProgress);

#if NET5_0_OR_GREATER
    await response.Content.CopyToAsync(destinationStream, null, cancellationToken).ConfigureAwait(false);
#else
    await response.Content.CopyToAsync(destinationStream).ConfigureAwait(false);
#endif
    destination.Refresh();
  }
}
