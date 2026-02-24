using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Helpers;

namespace Speckle.Sdk.Pipelines.Progress;

public partial interface IIngestionProgressManager : IProgress<CardProgress>;

/// <summary>
/// An <see langword="IProgress{IngestionProgressEventArgs}"/> implementation for the entire client side Ingestion progress update reporting
/// Will throttles ingestion progress messages and reports their progress
/// </summary>
[GenerateAutoInterface]
public sealed class IngestionProgressManager(
  ILogger<IngestionProgressManager> logger,
  IClient speckleClient,
  ModelIngestion ingestion,
  string projectId,
  TimeSpan updateInterval,
  CancellationToken cancellationToken
) : IIngestionProgressManager
{
  /// <remarks>
  /// Normally we would pick quite a coarse throttle window to try and avoid over pressure (1-5s)
  /// </remarks>
  private Task? _lastUpdate;
  private long _lastUpdatedAt;
  private readonly object _lock = new();

  [AutoInterfaceIgnore]
  public void Report(CardProgress value)
  {
    cancellationToken.ThrowIfCancellationRequested();

    string trimmedMessage;
    lock (_lock)
    {
      if (ShouldIgnoreProgressUpdate())
      {
        return;
      }

      _lastUpdatedAt = Stopwatch.GetTimestamp();

      trimmedMessage = value.Status.TrimEnd('.');

      _lastUpdate = speckleClient
        .Ingestion.UpdateProgress(
          new ModelIngestionUpdateInput(ingestion.id, projectId, trimmedMessage, value.Progress),
          cancellationToken
        )
        .ContinueWith(
          HandleFaultedContinuation,
          CancellationToken.None,
          TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
          TaskScheduler.Default
        );
    }

    logger.LogInformation("Progress update {Message} {Progress}", trimmedMessage, value.Progress);
  }

  /// <returns><see langword="true"/> if the update should be ignored, otherwise <see langword="false"/></returns>
  private bool ShouldIgnoreProgressUpdate()
  {
    if (_lastUpdate is not null && !_lastUpdate.IsCompleted)
    {
      return true;
    }

    TimeSpan msSinceLastUpdate = StopwatchPolyfills.GetElapsedTime(_lastUpdatedAt);
    return msSinceLastUpdate < updateInterval;
  }

  private void HandleFaultedContinuation(Task updateTask)
  {
    // The progress report failed... could be many reasons.
    // For now, we're not letting this fail the Ingestion in any way
    // we'll log but otherwise let it slide while leaving no unobserved task exceptions
    if (updateTask.IsFaulted)
    {
      logger.LogWarning(updateTask.Exception, "A progress update failed unexpectedly");
    }
  }
}
