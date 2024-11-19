using Polly;
using Polly.Contrib.WaitAndRetry;

namespace Speckle.Sdk.Dependencies;

public static class GraphQLRetry
{
  public static async Task<T> ExecuteAsync<T, TInnerException>(
    Func<Task<T>> func,
    Action<Exception, TimeSpan>? onRetry = null
  )
    where TInnerException : Exception
  {
    var delay = Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(1), 5);
    var graphqlRetry = Policy
      .HandleInner<TInnerException>()
      .WaitAndRetryAsync(
        delay,
        (ex, timeout, _) =>
        {
          onRetry?.Invoke(ex, timeout);
        }
      );

    return await graphqlRetry.ExecuteAsync(func).ConfigureAwait(false);
  }
}
