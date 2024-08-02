namespace Speckle.Logging;

/// <summary>
/// Configuration object for the Speckle logging system.
/// </summary>

public record SpeckleLogging(
  SpeckleLogLevel MinimumLevel = SpeckleLogLevel.Warning,
  bool Console = true,
  bool Seq = true,
  bool File = true,
  bool Otel = true
);

public record SpeckleTracing(bool Console = false, bool Seq = true, bool Otel = false);
