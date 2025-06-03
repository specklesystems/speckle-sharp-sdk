using System.Net;
using Polly;
using Polly.Retry;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Logging;

namespace Speckle.Sdk.Helpers;

[GenerateAutoInterface]
public sealed class SpeckleHttpClientHandlerFactory(ISdkActivityFactory activityFactory)
  : ISpeckleHttpClientHandlerFactory
{
  public const int DEFAULT_TIMEOUT_SECONDS = 60;


  private static ValueTask<bool> HandleTransientHttpError(Outcome<HttpResponseMessage> outcome) => outcome switch
  {
    { Exception: HttpRequestException } => PredicateResult.True(),
    { Result.StatusCode: HttpStatusCode.RequestTimeout } => PredicateResult.True(),
    { Result.StatusCode: >= HttpStatusCode.InternalServerError } => PredicateResult.True(),
    _ => PredicateResult.False()
  };

  private static RetryStrategyOptions<HttpResponseMessage> GetRetryOptions() =>
    new()
    {
      ShouldHandle = args => HandleTransientHttpError(args.Outcome),
      MaxRetryAttempts = 5,
      BackoffType = DelayBackoffType.Exponential,
      UseJitter = true,  // Adds a random factor to the delay
      Delay = TimeSpan.FromMilliseconds(200)
    };

  internal ResiliencePipeline<HttpResponseMessage> HttpAsyncPolicy(
    int timeoutSeconds = DEFAULT_TIMEOUT_SECONDS
  )
  {
    var pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
      .AddRetry(GetRetryOptions()) 
      .AddTimeout(TimeSpan.FromSeconds(timeoutSeconds))
      .Build();


    return pipeline;
  }

  public DelegatingHandler Create(
    HttpMessageHandler? innerHandler = null,
    int timeoutSeconds = DEFAULT_TIMEOUT_SECONDS
  ) =>
    new SpeckleHttpClientHandler(
      innerHandler ?? new HttpClientHandler(),
      activityFactory,
      HttpAsyncPolicy(timeoutSeconds: timeoutSeconds)
    );
}
