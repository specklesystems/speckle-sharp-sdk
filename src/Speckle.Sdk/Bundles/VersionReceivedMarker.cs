using Microsoft.Extensions.Logging;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Api;
using Speckle.Sdk.Credentials;

namespace Speckle.Sdk.Bundles;

/// <summary>
/// Records a successful receive on the server (<c>versionMutations.markReceived</c>) — the "received" activity the
/// web app and usage metrics show per version. Telemetry, not data: a failure here is logged and never fails the
/// receive that already succeeded.
/// </summary>
[GenerateAutoInterface]
public sealed class VersionReceivedMarker(
  IClientFactory clientFactory,
  ISpeckleApplication application,
  ILogger<VersionReceivedMarker> logger
) : IVersionReceivedMarker
{
  public async Task MarkAsync(Account account, string projectId, string versionId, CancellationToken cancellationToken)
  {
    try
    {
      using var client = clientFactory.Create(account);
      await client
        .Version.Received(new(versionId, projectId, application.Slug), cancellationToken)
        .ConfigureAwait(false);
    }
    catch (OperationCanceledException)
    {
      throw;
    }
    catch (Exception ex) when (ex is SpeckleException or HttpRequestException or AggregateException)
    {
      logger.LogWarning(
        ex,
        "Could not mark version {versionId} of project {projectId} as received",
        versionId,
        projectId
      );
    }
  }
}
