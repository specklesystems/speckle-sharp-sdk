using Polly;
using Speckle.Sdk.Common;
using Speckle.Sdk.Logging;

namespace Speckle.Sdk.Helpers;

public sealed class SpeckleHttpClientHandler : DelegatingHandler
{
  private readonly IAsyncPolicy<HttpResponseMessage> _resiliencePolicy;
  private readonly ISdkActivityFactory _activityFactory;

  public SpeckleHttpClientHandler(
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
      if (policyResult.FinalException != null)
      {
        activity?.RecordException(policyResult.FinalException);
        activity?.SetStatus(SdkActivityStatusCode.Error);
      }
      else
      {
        activity?.SetStatus(
          policyResult.Result.IsSuccessStatusCode ? SdkActivityStatusCode.Ok : SdkActivityStatusCode.Error
        );
      }

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
