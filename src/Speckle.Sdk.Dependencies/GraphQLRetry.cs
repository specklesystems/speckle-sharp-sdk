using Polly;
using Polly.Retry;

namespace Speckle.Sdk.Dependencies;

public static class GraphQLRetry
{
  public static async Task<T> ExecuteAsync<T, TInnerException>(
    Func<Task<T>> func,
    Func<Exception?, TimeSpan?, Task>? onRetry = null
  )
    where TInnerException : Exception
  {
    var options = new RetryStrategyOptions<T>
    {
      ShouldHandle = new PredicateBuilder<T>().Handle<TInnerException>(),
      BackoffType = DelayBackoffType.Exponential,
      UseJitter = true,  // Adds a random factor to the delay
      MaxRetryAttempts = 5,
      Delay = TimeSpan.FromSeconds(1),
    };
    if (onRetry != null)
    {
      options.OnRetry = x => new ValueTask(onRetry.Invoke(x.Outcome.Exception, x.Duration));
    }
    ResiliencePipeline<T> pipeline = new ResiliencePipelineBuilder<T>()
      .AddRetry(options) // Add retry using the default options
      .AddTimeout(TimeSpan.FromSeconds(10)) // Add 10 seconds timeout
      .Build(); // Builds the resilience pipeline

    return await pipeline.ExecuteAsync( _ => new ValueTask<T>(func())).ConfigureAwait(false);
  }
}
