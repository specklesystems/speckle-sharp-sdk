using System.Diagnostics.CodeAnalysis;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace Speckle.Sdk.Api.GraphQL;

/// <summary>
/// Re-establishes a GraphQL subscription stream after the transport drops it.
/// </summary>
/// <remarks>
/// <c>GraphQLHttpClient</c> only recreates a subscription for a <see cref="System.Net.WebSockets.WebSocketException"/>,
/// and only when a <c>webSocketExceptionHandler</c> is supplied; every other exception is terminal for the sequence.
/// A server that closes the socket cleanly surfaces as a plain <see cref="Exception"/> ("Connection closed by the
/// server."), so an orderly server shutdown killed the subscription permanently while an abrupt TCP teardown did not.
/// Retrying on any error makes both paths recoverable.
/// </remarks>
internal static class SubscriptionReconnect
{
  internal static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(1);
  internal static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(30);

  /// <summary>
  /// Exponential backoff, capped at <see cref="MaxDelay"/>, with up to <paramref name="jitterFactor"/> of the delay
  /// shaved off.
  /// </summary>
  /// <remarks>
  /// The jitter matters more than the curve here: a server rollout closes every subscriber's socket at once, so an
  /// undithered backoff would march the whole fleet back in lockstep.
  /// </remarks>
  /// <param name="attempt">1 for the first retry after a successful run.</param>
  /// <param name="jitterFactor">In [0,1). The fraction of the computed delay to subtract.</param>
  internal static TimeSpan GetDelay(int attempt, double jitterFactor)
  {
    if (attempt < 1)
    {
      attempt = 1;
    }

    // Shift rather than Math.Pow so a long-lived retry loop cannot overflow into a negative TimeSpan.
    long multiplier = attempt >= 31 ? int.MaxValue : 1 << (attempt - 1);
    var ticks = Math.Min(InitialDelay.Ticks * multiplier, MaxDelay.Ticks);
    return TimeSpan.FromTicks((long)(ticks * (1 - jitterFactor)));
  }

  /// <summary>
  /// Wraps <paramref name="streamFactory"/> so that any error resubscribes to a freshly created stream after a backoff.
  /// The attempt counter resets whenever the stream delivers a value, so an intermittent drop is always retried
  /// promptly rather than inheriting the delay of an earlier outage.
  /// </summary>
  /// <param name="streamFactory">Creates a new subscription stream. Called once per connection attempt.</param>
  /// <param name="onReconnecting">Invoked with the error and the delay before each retry.</param>
  /// <param name="jitterSource">Supplies the jitter factor in [0,1). Defaults to a shared random.</param>
  /// <param name="scheduler">Schedules the backoff. Defaults to <see cref="DefaultScheduler"/>.</param>
  internal static IObservable<T> WithReconnect<T>(
    Func<IObservable<T>> streamFactory,
    Action<Exception, TimeSpan> onReconnecting,
    Func<double>? jitterSource = null,
    IScheduler? scheduler = null
  )
  {
    var jitter = jitterSource ?? DefaultJitter;
    var effectiveScheduler = scheduler ?? DefaultScheduler.Instance;
    var attempts = new Attempts();

    return Observable
      .Defer(streamFactory)
      .Do(_ => attempts.Reset())
      .RetryWhen(errors =>
        errors.SelectMany(ex =>
        {
          var delay = GetDelay(attempts.Next(), jitter());
          onReconnecting(ex, delay);
          return Observable.Return(Unit.Default).Delay(delay, effectiveScheduler);
        })
      );
  }

  private sealed class Attempts
  {
    private int _count;

    internal int Next() => Interlocked.Increment(ref _count);

    internal void Reset() => Interlocked.Exchange(ref _count, 0);
  }

#if NET6_0_OR_GREATER
  [SuppressMessage(
    "Security",
    "CA5394:Do not use insecure randomness",
    Justification = "Jitter spreads reconnect attempts across clients, it is not a security boundary"
  )]
  private static double DefaultJitter() => Random.Shared.NextDouble() * 0.5;
#else
  private static readonly Random _random = new();

  [SuppressMessage(
    "Security",
    "CA5394:Do not use insecure randomness",
    Justification = "Jitter spreads reconnect attempts across clients, it is not a security boundary"
  )]
  private static double DefaultJitter()
  {
    // Random is not thread safe and a stream can fault on any transport thread.
    lock (_random)
    {
      return _random.NextDouble() * 0.5;
    }
  }
#endif
}
