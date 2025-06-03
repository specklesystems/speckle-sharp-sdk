using Polly;
using Speckle.Sdk.Logging;

namespace Speckle.Sdk.Helpers;

internal sealed class SpeckleHttpClientHandler : DelegatingHandler
{
  private readonly ResiliencePipeline<HttpResponseMessage> _resiliencePolicy;
  private readonly ISdkActivityFactory _activityFactory;

  internal SpeckleHttpClientHandler(
    HttpMessageHandler innerHandler,
    ISdkActivityFactory activityFactory,
    ResiliencePipeline<HttpResponseMessage> resiliencePolicy
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
    ResilienceContext context = ResilienceContextPool.Shared.Get(cancellationToken);
    using var activity = _activityFactory.Start("Http Request");
    {
      activity?.SetTag("http.method", request.Method);
      activity?.SetTag("http.url", request.RequestUri);
      activity?.SetTag("correlationId", context.OperationKey);

      context.Properties.Set(new ResiliencePropertyKey<int>("retryCount"), 0);

      request.Headers.Add("x-request-id", context.OperationKey);
      activity?.InjectHeaders((k, v) => request.Headers.TryAddWithoutValidation(k, v));

      var policyResult = await _resiliencePolicy.ExecuteOutcomeAsync<HttpResponseMessage, string>(
        async (ctx, _) =>
        {
          try
          {
            var message = await base.SendAsync(request, ctx.CancellationToken).ConfigureAwait(false);
            return Outcome.FromResult(message);
          }
#pragma warning disable CA1031
          catch (Exception e)
#pragma warning restore CA1031
          {
            return Outcome.FromException<HttpResponseMessage>(e);
          }
        },
        context,
        "state"
      ).ConfigureAwait(false);
      context.Properties.TryGetValue(new ResiliencePropertyKey<int>("retryCount"), out var retryCount);
      activity?.SetTag("retryCount", retryCount);

      if (policyResult.Result is not null)
      {
        activity?.SetStatus(SdkActivityStatusCode.Ok);
        return policyResult.Result;
      }

      activity?.SetStatus(SdkActivityStatusCode.Error);

      if (policyResult.Exception is not null)
      {
        activity?.RecordException(policyResult.Exception);
      }

      // if the policy failed due to a cancellation, AND it was our cancellation token, then don't wrap the exception, and rethrow an new cancellation
      if (policyResult.Exception is OperationCanceledException)
      {
        cancellationToken.ThrowIfCancellationRequested();
      }

      throw new HttpRequestException("Policy Failed", policyResult.Exception);
    }
  }
}
