using Microsoft.Extensions.Logging;

namespace Speckle.Logging;

/// <summary>
/// Configuration object for the Speckle logging system.
/// </summary>
public sealed class SpeckleLogConfiguration
{
  /// <summary>
  /// Flag to enable enhanced log context. This adds the following enrich calls:
  /// - WithClientAgent
  /// - WithClientIp
  /// - WithExceptionDetails
  /// </summary>
  public bool EnhancedLogContext { get; }

  /// <summary>
  /// Flag to enable console sink
  /// </summary>
  public bool LogToConsole { get; }

  /// <summary>
  /// Flag to enable File sink
  /// </summary>
  public bool LogToFile { get; }

  /// <summary>
  /// Flag to enable Seq sink
  /// </summary>
  public bool LogToSeq { get; }

  /// <summary>
  /// Log events bellow this level are silently dropped
  /// </summary>
  public LogLevel MinimumLevel { get; }

  /// <summary>
  /// Default SpeckleLogConfiguration constructor.
  /// These are the sane defaults we should be using across connectors.
  /// </summary>
  /// <param name="minimumLevel">Log events bellow this level are silently dropped</param>
  /// <param name="logToConsole">Flag to enable console log sink</param>
  /// <param name="logToSeq">Flag to enable Seq log sink</param>
  /// <param name="logToFile">Flag to enable File log sink</param>
  /// <param name="enhancedLogContext">Flag to enable enhanced context on every log event</param>
  public SpeckleLogConfiguration(
    LogLevel minimumLevel = LogLevel.Warning,
    bool logToConsole = true,
    bool logToSeq = true,
    bool logToFile = true,
    bool enhancedLogContext = true
  )
  {
    MinimumLevel = minimumLevel;
    LogToConsole = logToConsole;
    LogToSeq = logToSeq;
    LogToFile = logToFile;
    EnhancedLogContext = enhancedLogContext;
  }
}
