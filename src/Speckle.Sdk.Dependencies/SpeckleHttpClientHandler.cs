using Polly;
using Speckle.Sdk.Logging;

namespace Speckle.Sdk.Helpers;

internal sealed class SpeckleHttpClientHandler : DelegatingHandler
{
  private readonly IAsyncPolicy<HttpResponseMessage> _resiliencePolicy;
  private readonly ISdkActivityFactory _activityFactory;

  internal SpeckleHttpClientHandler(
    HttpMessageHandler innerHandler,
    ISdkActivityFactory activityFactory,
    IAsyncPolicy<HttpResponseMessage> resiliencePolicy
  )
    : base(innerHandler)
  {
    _activityFactory = activityFactory;
    _resiliencePolicy = resiliencePolicy;
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
    var context = new Context();
    using var activity = _activityFactory.Start("Http Request");
    {
      activity?.SetTag("http.method", request.Method);
      activity?.SetTag("http.url", request.RequestUri);
      activity?.SetTag("correlationId", context.CorrelationId);

      context.Add("retryCount", 0);

      request.Headers.Add("x-request-id", context.CorrelationId.ToString());
      activity?.InjectHeaders((k, v) => request.Headers.TryAddWithoutValidation(k, v));

      var policyResult = await _resiliencePolicy
        .ExecuteAndCaptureAsync(ctx => base.SendAsync(request, cancellationToken), context)
        .ConfigureAwait(false);
      context.TryGetValue("retryCount", out var retryCount);
      activity?.SetTag("retryCount", retryCount);

      if (policyResult.Outcome == OutcomeType.Successful)
      {
        activity?.SetStatus(SdkActivityStatusCode.Ok);
        return policyResult.Result;
      }

      activity?.SetStatus(SdkActivityStatusCode.Error);

      if (policyResult.FinalException is null)
      {
        // Outcome was not successful, but did not terminate with an exception (e.g. repeated 500 responses)
        return policyResult.FinalHandledResult;
      }

      activity?.RecordException(policyResult.FinalException);

      // if the policy failed due to a cancellation, AND it was our cancellation token, then don't wrap the exception, and rethrow an new cancellation
      if (policyResult.FinalException is OperationCanceledException)
      {
        cancellationToken.ThrowIfCancellationRequested();
      }

      throw new HttpRequestException("Policy Failed", policyResult.FinalException);
    }
  }
}
