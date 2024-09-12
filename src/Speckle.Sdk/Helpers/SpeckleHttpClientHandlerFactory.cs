﻿using Microsoft.Extensions.Logging;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Extensions.Http;
using Polly.Timeout;
using Speckle.InterfaceGenerator;

namespace Speckle.Sdk.Helpers;

[GenerateAutoInterface]
public sealed class SpeckleHttpClientHandlerFactory(ILoggerFactory loggerFactory) : ISpeckleHttpClientHandlerFactory
{
  public const int DEFAULT_TIMEOUT_SECONDS = 60;

  public IEnumerable<TimeSpan> DefaultDelay()
  {
    return Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromMilliseconds(200), 5);
  }

  public IAsyncPolicy<HttpResponseMessage> HttpAsyncPolicy(
    IEnumerable<TimeSpan>? delay = null,
    int timeoutSeconds = DEFAULT_TIMEOUT_SECONDS
  )
  {
    var retryPolicy = HttpPolicyExtensions
      .HandleTransientHttpError()
      .Or<TimeoutRejectedException>()
      .WaitAndRetryAsync(
        delay ?? DefaultDelay(),
        (ex, timeSpan, retryAttempt, context) =>
        {
          context.Remove("retryCount");
          context.Add("retryCount", retryAttempt);
        }
      );

    var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(timeoutSeconds);

    return Policy.WrapAsync(retryPolicy, timeoutPolicy);
  }

  public SpeckleHttpClientHandler Create(
    HttpMessageHandler? innerHandler = null,
    IAsyncPolicy<HttpResponseMessage>? resiliencePolicy = null,
  int timeoutSeconds = DEFAULT_TIMEOUT_SECONDS
  ) =>
    new(
      innerHandler ?? new HttpClientHandler(),
      resiliencePolicy ?? HttpAsyncPolicy(timeoutSeconds:timeoutSeconds),
      loggerFactory.CreateLogger<SpeckleHttpClientHandler>()
    );
}
