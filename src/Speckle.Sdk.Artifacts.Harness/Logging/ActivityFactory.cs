using System.Diagnostics;
using System.Reflection;

namespace Speckle.Sdk.Artifacts.Harness.Logging;

internal static class Telemetry
{
  public static ActivitySource Activity { get; } =
    new(LoggingConfiguration.TRACING_SOURCE, LoggingConfiguration.GetPackageVersion(Assembly.GetExecutingAssembly()));
}
