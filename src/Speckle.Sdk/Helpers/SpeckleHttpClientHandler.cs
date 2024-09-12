using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Polly;
using Speckle.Sdk.Common;
using Speckle.Sdk.Logging;

namespace Speckle.Sdk.Helpers;

public sealed class SpeckleHttpClientHandler : DelegatingHandler
{
  private readonly IAsyncPolicy<HttpResponseMessage> _resiliencePolicy;
  private readonly ILogger<SpeckleHttpClientHandler> _logger;
  private readonly IActivityFactory _activityFactory;

  public SpeckleHttpClientHandler(
    HttpMessageHandler innerHandler,
    IActivityFactory activityFactory,
    IAsyncPolicy<HttpResponseMessage> resiliencePolicy,
    ILogger<SpeckleHttpClientHandler> logger
  )
    : base(innerHandler)
  {
    _activityFactory = activityFactory;
    _resiliencePolicy = resiliencePolicy;
    _logger = logger;
  }

  /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> requested cancel</exception>
  /// <exception cref="HttpRequestException">Send request failed</exception>
  protected override async Task<HttpResponseMessage> SendAsync(
    HttpRequestMessage request,
    CancellationToken cancellationToken
  )
  {
    // this is a preliminary client server correlation implementation
    // refactor this, when we have a better observability stack
    var sw = Stopwatch.StartNew();
    var context = new Context();
    using var activity = _activityFactory.Start("Http Send");
    {
      _logger.LogDebug(
        "Starting execution of http request to {targetUrl} {correlationId} {traceId}",
        request.RequestUri,
        context.CorrelationId,
        activity?.TraceId
      );
      activity?.SetTag("http.url", request.RequestUri);
      activity?.SetTag("correlationId", context.CorrelationId);

      context.Add("retryCount", 0);

      request.Headers.Add("x-request-id", context.CorrelationId.ToString());

      var policyResult = await _resiliencePolicy
        .ExecuteAndCaptureAsync(
          ctx =>
          {
            return base.SendAsync(request, cancellationToken);
          },
          context
        )
        .ConfigureAwait(false);
      context.TryGetValue("retryCount", out var retryCount);
      activity?.SetTag("retryCount", retryCount);

      _logger.LogInformation(
        "Execution of http request to {url} {resultStatus} with {httpStatusCode} after {elapsed} seconds and {retryCount} retries. Request correlation ID: {correlationId}",
        request.RequestUri,
        policyResult.Outcome == OutcomeType.Successful ? "succeeded" : "failed",
        policyResult.Result?.StatusCode,
        sw.Elapsed.TotalSeconds,
        retryCount ?? 0,
        context.CorrelationId.ToString()
      );

      if (policyResult.Outcome == OutcomeType.Successful)
      {
        return policyResult.Result.NotNull();
      }

      // if the policy failed due to a cancellation, AND it was our cancellation token, then don't wrap the exception, and rethrow an new cancellation
      if (policyResult.FinalException is OperationCanceledException)
      {
        cancellationToken.ThrowIfCancellationRequested();
      }

      throw new HttpRequestException("Policy Failed", policyResult.FinalException);
    }
  }
}
