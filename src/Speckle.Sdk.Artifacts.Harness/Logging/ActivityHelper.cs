using System.Diagnostics;
using Speckle.Connectors.Logging;
using Speckle.Sdk.Logging;

namespace Speckle.Sdk.Artifacts.Harness.Logging;

internal static class ActivityHelper
{
  public static ISdkActivity? StartActivityFromEnv(this ISdkActivityFactory activityFactory)
  {
    var traceParent = Environment.GetEnvironmentVariable("SPECKLE_OTEL_TRACEPARENT");
    var traceState = Environment.GetEnvironmentVariable("SPECKLE_OTEL_TRACESTATE");
    return activityFactory.StartRemote(traceParent, traceState, SdkActivityKind.Server, "Automation Runner");
  }

  /// <summary>
  /// Propagates trace context from server via environment variables
  /// </summary>
  /// <returns></returns>
  public static Activity? StartActivityFromEnv()
  {
    var traceParent = Environment.GetEnvironmentVariable("SPECKLE_OTEL_TRACEPARENT");
    var traceState = Environment.GetEnvironmentVariable("SPECKLE_OTEL_TRACESTATE");
    ActivityContext context = ActivityContext.TryParse(
      traceParent,
      traceState,
      traceParent is not null,
      out ActivityContext propagatedContext
    )
      ? propagatedContext
      : default;

    return Telemetry.Activity.StartActivity(
      "Running Automate Function",
      ActivityKind.Server,
      context,
      new Dictionary<string, object?>([.. ActivityContexts.UserInfoContext(null!)])
    );
  }
}
