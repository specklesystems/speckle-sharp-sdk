using System.Diagnostics;
using Polly;
using Speckle.Sdk.Common;
using Speckle.Sdk.Logging;

namespace Speckle.Sdk.Helpers;

public sealed class SpeckleHttpClientHandler : DelegatingHandler
{
  private readonly IEnumerable<TimeSpan> _delay;

  public SpeckleHttpClientHandler(HttpMessageHandler innerhandler, IEnumerable<TimeSpan>? delay = null)
    : base(innerhandler)
  {
    _delay = delay ?? Http.DefaultDelay();
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
    using var activity = SpeckleActivityFactory.Start("Http Send");
    {
      SpeckleLog.Logger.Debug(
        "Starting execution of http request to {targetUrl} {correlationId} {traceId}",
        request.RequestUri,
        context.CorrelationId,
        activity?.TraceId
      );
      activity?.SetTag("http.url", request.RequestUri);
      activity?.SetTag("correlationId", context.CorrelationId);

      context.Add("retryCount", 0);

      request.Headers.Add("x-request-id", context.CorrelationId.ToString());

      var policyResult = await Http.HttpAsyncPolicy(_delay)
        .ExecuteAndCaptureAsync(
          ctx =>
          {
            try
            {
              return base.SendAsync(request, cancellationToken);
            }
            catch (TaskCanceledException ex)
            {
              if (ex.CancellationToken == cancellationToken)
              {
                cancellationToken.ThrowIfCancellationRequested();
              }

              throw;
            }
          },
          context
        )
        .ConfigureAwait(false);
      context.TryGetValue("retryCount", out var retryCount);
      activity?.SetTag("retryCount", retryCount);

      SpeckleLog.Logger.Information(
        "Execution of http request to {httpScheme}://{hostUrl}{relativeUrl} {resultStatus} with {httpStatusCode} after {elapsed} seconds and {retryCount} retries. Request correlation ID: {correlationId}",
        request.RequestUri.Scheme,
        request.RequestUri.Host,
        request.RequestUri.PathAndQuery,
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
