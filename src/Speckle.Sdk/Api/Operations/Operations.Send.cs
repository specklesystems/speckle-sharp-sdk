using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Speckle.Newtonsoft.Json.Linq;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;
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
  /// Sends a Speckle Object to the provided <paramref name="transport"/> and (optionally) the default local cache
  /// </summary>
  /// <remarks/>
  /// <inheritdoc cref="Send(Base, IReadOnlyCollection{ITransport}, IProgress{ProgressArgs}?, CancellationToken)"/>
  /// <param name="useDefaultCache">When <see langword="true"/>, an additional <see cref="SQLiteTransport"/> will be included</param>
  /// <exception cref="ArgumentNullException">The <paramref name="transport"/> or <paramref name="value"/> was <see langword="null"/></exception>
  /// <example><code>
  /// using ServerTransport destination = new(account, streamId);
  /// var (objectId, references) = await Send(mySpeckleObject, destination, true);
  /// </code></example>
  public async Task<(string rootObjId, IReadOnlyDictionary<string, ObjectReference> convertedReferences)> Send(
    Base value,
    IServerTransport transport,
    bool useDefaultCache,
    IProgress<ProgressArgs>? onProgressAction = null,
    CancellationToken cancellationToken = default
  )
  {
    if (transport is null)
    {
      throw new ArgumentNullException(nameof(transport), "Expected a transport to be explicitly specified");
    }

    List<ITransport> transports = new() { transport };
    using SQLiteTransport2? localCache = useDefaultCache
      ? new SQLiteTransport2(transport.StreamId) { TransportName = "LC2" }
      : null;
    if (localCache is not null)
    {
      transports.Add(localCache);
    }

    return await Send(value, transports, onProgressAction, cancellationToken).ConfigureAwait(false);
  }

  /// <summary>
  /// Sends a Speckle Object to the provided <paramref name="transport"/> and (optionally) the default local cache
  /// </summary>
  /// <remarks/>
  /// <inheritdoc cref="Send(Base, IReadOnlyCollection{ITransport}, IProgress{ProgressArgs}?, CancellationToken)"/>
  /// <param name="useDefaultCache">When <see langword="true"/>, an additional <see cref="SQLiteTransport"/> will be included</param>
  /// <exception cref="ArgumentNullException">The <paramref name="transport"/> or <paramref name="value"/> was <see langword="null"/></exception>
  /// <example><code>
  /// using ServerTransport destination = new(account, streamId);
  /// var (objectId, references) = await Send(mySpeckleObject, destination, true);
  /// </code></example>
  public async Task<(string rootObjId, IReadOnlyDictionary<string, ObjectReference> convertedReferences)> Send(
    Base value,
    ITransport transport,
    bool useDefaultCache,
    IProgress<ProgressArgs>? onProgressAction = null,
    CancellationToken cancellationToken = default
  )
  {
    if (transport is null)
    {
      throw new ArgumentNullException(nameof(transport), "Expected a transport to be explicitly specified");
    }

    List<ITransport> transports = new() { transport };
    using SQLiteTransport? localCache = useDefaultCache ? new SQLiteTransport { TransportName = "LC" } : null;
    if (localCache is not null)
    {
      transports.Add(localCache);
    }

    return await Send(value, transports, onProgressAction, cancellationToken).ConfigureAwait(false);
  }

  /// <summary>
  /// Sends a Speckle Object to the provided <paramref name="transports"/>
  /// </summary>
  /// <remarks>Only sends to the specified transports, the default local cache won't be used unless you also pass it in</remarks>
  /// <returns>The id (hash) of the object sent</returns>
  /// <param name="value">The object you want to send</param>
  /// <param name="transports">Where you want to send them</param>
  /// <param name="onProgressAction">Action that gets triggered on every progress tick (keeps track of all transports)</param>
  /// <param name="cancellationToken"></param>
  /// <exception cref="ArgumentException">No transports were specified</exception>
  /// <exception cref="ArgumentNullException">The <paramref name="value"/> was <see langword="null"/></exception>
  /// <exception cref="SpeckleException">Serialization or Send operation was unsuccessful</exception>
  /// <exception cref="TransportException">One or more <paramref name="transports"/> failed to send</exception>
  /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> requested cancellation</exception>
  public async Task<(string rootObjId, IReadOnlyDictionary<string, ObjectReference> convertedReferences)> Send(
    Base value,
    IReadOnlyCollection<ITransport> transports,
    IProgress<ProgressArgs>? onProgressAction = null,
    CancellationToken cancellationToken = default
  )
  {
#pragma warning disable CA1510
    if (value is null)
    {
      throw new ArgumentNullException(nameof(value));
    }
#pragma warning restore CA1510

    if (transports.Count == 0)
    {
      throw new ArgumentException("Expected at least on transport to be specified", nameof(transports));
    }

    // make sure all logs in the operation have the proper context
    metricsFactory.CreateCounter<long>("Send").Add(1);
    using var activity = activityFactory.Start();
    activity?.SetTag("correlationId", Guid.NewGuid().ToString());
    {
      var sendTimer = Stopwatch.StartNew();
      logger.LogDebug("Starting send operation");

      SpeckleObjectSerializer serializerV2 = new(transports, onProgressAction, true, cancellationToken);

      foreach (var t in transports)
      {
        t.OnProgressAction = onProgressAction;
        t.CancellationToken = cancellationToken;
        t.BeginWrite();
      }

      try
      {
        var rootObjectId = await SerializerSend(value, serializerV2, cancellationToken).ConfigureAwait(false);

        sendTimer.Stop();
        activity?.SetTags("transportElapsedBreakdown", transports.ToDictionary(t => t.TransportName, t => t.Elapsed));
        activity?.SetTag(
          "note",
          "the elapsed summary doesn't need to add up to the total elapsed... Threading magic..."
        );
        activity?.SetTag("serializerElapsed", serializerV2.Elapsed);
        logger.LogDebug(
          "Finished sending objects after {elapsed}, result {objectId}",
          sendTimer.Elapsed.TotalSeconds,
          rootObjectId
        );

        return (rootObjectId, serializerV2.ObjectReferences);
      }
      catch (Exception ex) when (!ex.IsFatal())
      {
        logger.LogInformation(ex, "Send operation failed after {elapsed} seconds", sendTimer.Elapsed.TotalSeconds);
        if (ex is OperationCanceledException or SpeckleException)
        {
          throw;
        }

        throw new SpeckleException("Send operation was unsuccessful", ex);
      }
      finally
      {
        foreach (var t in transports)
        {
          t.EndWrite();
        }
      }
    }
  }

  /// <returns><inheritdoc cref="Send(Base, IReadOnlyCollection{ITransport}, IProgress{ProgressArgs}?, CancellationToken)"/></returns>
  internal static async Task<string> SerializerSend(
    Base value,
    SpeckleObjectSerializer serializer,
    CancellationToken cancellationToken = default
  )
  {
    string obj = serializer.Serialize(value);
    Task[] transportAwaits = serializer.WriteTransports.Select(t => t.WriteComplete()).ToArray();

    cancellationToken.ThrowIfCancellationRequested();

    await Task.WhenAll(transportAwaits).ConfigureAwait(false);

    JToken? idToken = JObject.Parse(obj).GetValue("id");
    if (idToken == null)
    {
      throw new SpeckleException("Failed to get id of serialized object");
    }

    return idToken.ToString();
  }
}
