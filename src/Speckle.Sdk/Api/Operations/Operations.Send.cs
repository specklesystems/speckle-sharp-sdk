using Speckle.Sdk.Logging;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.V2.Send;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Api;

public partial class Operations
{
  /// <summary>
  /// Sends a Speckle Object to the provided URL and Caches the results
  /// </summary>
  /// <remarks/>
  /// <exception cref="ArgumentException">No transports were specified</exception>
  /// <exception cref="ArgumentNullException">The <paramref name="value"/> was <see langword="null"/></exception>
  /// <exception cref="SpeckleException">Serialization or Send operation was unsuccessful</exception>
  /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> requested cancellation</exception>
  public async Task<SerializeProcessResults> Send2(
    Uri url,
    string streamId,
    string? authorizationToken,
    Base value,
    IProgress<ProgressArgs>? onProgressAction,
    CancellationToken cancellationToken
  )
  {
    using var receiveActivity = activityFactory.Start("Operations.Send");
    receiveActivity?.SetTag("speckle.url", url);
    receiveActivity?.SetTag("speckle.projectId", streamId);
    metricsFactory.CreateCounter<long>("Send").Add(1);

    var process = serializeProcessFactory.CreateSerializeProcess(
      url,
      streamId,
      authorizationToken,
      onProgressAction,
      cancellationToken
    );
    try
    {
      var results = await process.Serialize(value).ConfigureAwait(false);

      receiveActivity?.SetStatus(SdkActivityStatusCode.Ok);
      return results;
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
}
