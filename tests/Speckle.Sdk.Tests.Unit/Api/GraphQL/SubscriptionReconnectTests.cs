using System.Net.WebSockets;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using AwesomeAssertions;
using Speckle.Sdk.Api.GraphQL;

namespace Speckle.Sdk.Tests.Unit.Api.GraphQL;

/// <summary>
/// The reconnect wrapper that keeps a subscription alive across transport drops. The case that motivated it is a
/// clean server-side close, which the GraphQL client surfaces as a plain <see cref="Exception"/> and treats as
/// terminal.
/// </summary>
public sealed class SubscriptionReconnectTests
{
  private const double NO_JITTER = 0d;

  [Fact]
  public void GetDelay_GrowsExponentiallyThenCaps()
  {
    SubscriptionReconnect.GetDelay(1, NO_JITTER).Should().Be(TimeSpan.FromSeconds(1));
    SubscriptionReconnect.GetDelay(2, NO_JITTER).Should().Be(TimeSpan.FromSeconds(2));
    SubscriptionReconnect.GetDelay(3, NO_JITTER).Should().Be(TimeSpan.FromSeconds(4));
    SubscriptionReconnect.GetDelay(6, NO_JITTER).Should().Be(TimeSpan.FromSeconds(30));
    SubscriptionReconnect.GetDelay(99, NO_JITTER).Should().Be(SubscriptionReconnect.MaxDelay);
  }

  [Fact]
  public void GetDelay_SubtractsJitter()
  {
    SubscriptionReconnect.GetDelay(3, 0.5d).Should().Be(TimeSpan.FromSeconds(2));
  }

  [Fact]
  public void WithReconnect_ResubscribesAfterACleanServerClose()
  {
    var scheduler = new HistoricalScheduler();
    var attempts = 0;
    var observed = new List<int>();

    using var subscription = SubscriptionReconnect
      .WithReconnect(
        () =>
        {
          attempts++;
          // The exact failure a graceful server shutdown produces: not a WebSocketException, so the GraphQL client
          // would end the sequence here.
          return attempts == 1
            ? Observable.Throw<int>(new InvalidOperationException("Connection closed by the server."))
            : Observable.Return(42);
        },
        (_, _) => { },
        () => NO_JITTER,
        scheduler
      )
      .Subscribe(observed.Add);

    scheduler.Start();

    attempts.Should().Be(2);
    observed.Should().Equal(42);
  }

  [Fact]
  public void WithReconnect_ResubscribesAfterAWebSocketException()
  {
    var scheduler = new HistoricalScheduler();
    var attempts = 0;
    var observed = new List<int>();

    using var subscription = SubscriptionReconnect
      .WithReconnect(
        () =>
        {
          attempts++;
          return attempts == 1
            ? Observable.Throw<int>(new WebSocketException(WebSocketError.ConnectionClosedPrematurely))
            : Observable.Return(7);
        },
        (_, _) => { },
        () => NO_JITTER,
        scheduler
      )
      .Subscribe(observed.Add);

    scheduler.Start();

    attempts.Should().Be(2);
    observed.Should().Equal(7);
  }

  [Fact]
  public void WithReconnect_BacksOffExponentiallyAcrossConsecutiveFailures()
  {
    var scheduler = new HistoricalScheduler();
    var delays = new List<TimeSpan>();
    var attempts = 0;

    using var subscription = SubscriptionReconnect
      .WithReconnect(
        () =>
        {
          attempts++;
          return attempts <= 3 ? Observable.Throw<int>(new InvalidOperationException("boom")) : Observable.Never<int>();
        },
        (_, delay) => delays.Add(delay),
        () => NO_JITTER,
        scheduler
      )
      .Subscribe(_ => { });

    scheduler.Start();

    delays.Should().Equal(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4));
  }

  [Fact]
  public void WithReconnect_ResetsBackoffAfterAValueIsDelivered()
  {
    var scheduler = new HistoricalScheduler();
    var delays = new List<TimeSpan>();
    var attempts = 0;

    using var subscription = SubscriptionReconnect
      .WithReconnect(
        () =>
        {
          attempts++;
          return attempts switch
          {
            // Fail twice to climb the curve, then deliver a value before failing again.
            1 or 2 => Observable.Throw<int>(new InvalidOperationException("boom")),
            3 => Observable
              .Return(1)
              .Concat(Observable.Throw<int>(new InvalidOperationException("dropped after a good run"))),
            _ => Observable.Never<int>(),
          };
        },
        (_, delay) => delays.Add(delay),
        () => NO_JITTER,
        scheduler
      )
      .Subscribe(_ => { });

    scheduler.Start();

    // The third delay is back at the floor rather than continuing to 4s: a drop after a healthy run is not an outage.
    delays.Should().Equal(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1));
  }

  [Fact]
  public void WithReconnect_ReportsEveryDropToTheCallback()
  {
    var scheduler = new HistoricalScheduler();
    var reported = new List<string>();
    var attempts = 0;

    using var subscription = SubscriptionReconnect
      .WithReconnect(
        () =>
        {
          attempts++;
          return attempts <= 2
            ? Observable.Throw<int>(new InvalidOperationException($"drop {attempts}"))
            : Observable.Never<int>();
        },
        (ex, _) => reported.Add(ex.Message),
        () => NO_JITTER,
        scheduler
      )
      .Subscribe(_ => { });

    scheduler.Start();

    reported.Should().Equal("drop 1", "drop 2");
  }

  [Fact]
  public void WithReconnect_StopsRetryingOnceDisposed()
  {
    var scheduler = new HistoricalScheduler();
    var attempts = 0;

    var subscription = SubscriptionReconnect
      .WithReconnect(
        () =>
        {
          attempts++;
          return Observable.Throw<int>(new InvalidOperationException("boom"));
        },
        (_, _) => { },
        () => NO_JITTER,
        scheduler
      )
      .Subscribe(_ => { });

    scheduler.AdvanceBy(TimeSpan.FromSeconds(1));
    subscription.Dispose();
    var attemptsAtDisposal = attempts;

    scheduler.AdvanceBy(TimeSpan.FromMinutes(10));

    attempts.Should().Be(attemptsAtDisposal);
  }
}
