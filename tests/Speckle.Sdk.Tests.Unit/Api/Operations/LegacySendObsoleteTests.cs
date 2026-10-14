using System.Reflection;
using GraphQL;
using Microsoft.Extensions.Logging;
using Speckle.Sdk.Api.GraphQL;
using Speckle.Sdk.Api.GraphQL.Resources;

namespace Speckle.Sdk.Tests.Unit.Api.Operations;

/// <summary>ENG-9304 / ENG-9418: the legacy publish path warns once per process at runtime.</summary>
public sealed class LegacySendObsoleteTests
{
  private const string GUIDE_URL = "https://docs.speckle.systems/developers/migration/publish-through-ingestions";

  [Fact]
  public async Task VersionCreate_Warns_OncePerProcess()
  {
    // reset the process-wide flag so this test is order-independent
    typeof(VersionResource)
      .GetField("s_legacyCreateWarned", BindingFlags.NonPublic | BindingFlags.Static)!
      .SetValue(null, 0);
    var logger = new CountingLogger();
    var resource = new VersionResource(new ThrowingGraphQLClient(), logger);

#pragma warning disable CS0618 // the obsolete surface is exactly what this test covers
    await Assert.ThrowsAsync<NotSupportedException>(() => resource.Create(new("obj", "model", "proj")));
    await Assert.ThrowsAsync<NotSupportedException>(() => resource.Create(new("obj", "model", "proj")));
#pragma warning restore CS0618

    Assert.Equal(1, logger.Warnings);
    Assert.Contains(GUIDE_URL, logger.LastMessage, StringComparison.Ordinal);
  }

  private sealed class ThrowingGraphQLClient : ISpeckleGraphQLClient
  {
    Task<T> ISpeckleGraphQLClient.ExecuteGraphQLRequest<T>(GraphQLRequest request, CancellationToken ct) =>
      throw new NotSupportedException("no network in unit tests");

    IDisposable ISpeckleGraphQLClient.SubscribeTo<T>(GraphQLRequest request, Action<object, T> callback) =>
      throw new NotSupportedException();
  }

  private sealed class CountingLogger : ILogger
  {
    public int Warnings;
    public string LastMessage = "";

    public IDisposable? BeginScope<TState>(TState state)
      where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
      LogLevel logLevel,
      EventId eventId,
      TState state,
      Exception? exception,
      Func<TState, Exception?, string> formatter
    )
    {
      if (logLevel == LogLevel.Warning)
      {
        Warnings++;
        LastMessage = formatter(state, exception);
      }
    }
  }
}
