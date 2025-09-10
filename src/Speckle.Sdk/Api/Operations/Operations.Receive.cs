using Speckle.Sdk.Logging;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Api;

public partial class Operations
{
  /// <summary>
  /// Receives a Object to the provided URL and Caches the results
  /// </summary>
  /// <remarks/>
  /// <exception cref="ArgumentException">No transports were specified</exception>
  /// <exception cref="ArgumentNullException">The <paramref name="objectId"/> was <see langword="null"/></exception>
  /// <exception cref="SpeckleException">Serialization or Send operation was unsuccessful</exception>
  /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> requested cancellation</exception>
  public async Task<Base> Receive2(
    Uri url,
    string streamId,
    string objectId,
    string? authorizationToken,
    IProgress<ProgressArgs>? onProgressAction,
    CancellationToken cancellationToken
  )
  {
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
      cancellationToken
    );
    try
    {
      var result = await process.Deserialize(objectId).ConfigureAwait(false);
      receiveActivity?.SetStatus(SdkActivityStatusCode.Ok);
      return result;
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
}
