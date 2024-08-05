namespace Speckle.Logging;

/// <summary>
/// Configuration object for the Speckle logging system.
/// </summary>
public record SpeckleLogConfiguration(
  bool LogToConsole = true,
  bool LogToSeq = true,
  bool LogToFile = true,
  bool LogToOtel = true,
  bool EnhancedLogContext = true
);
