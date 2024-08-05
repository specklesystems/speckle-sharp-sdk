using Microsoft.Extensions.Logging;

namespace Speckle.Logging;

public static class LogExtensions
{
  public static void Debug(this ILogger logger, string message, params object?[] arguments) =>
    logger.LogDebug(message, arguments);

  public static void Debug(this ILogger logger, Exception exception, string message, params object?[] arguments) =>
    logger.LogDebug(exception, message, arguments);

  public static void Warning(this ILogger logger, string message, params object?[] arguments) =>
    logger.LogWarning(message, arguments);

  public static void Warning(this ILogger logger, Exception exception, string message, params object?[] arguments) =>
    logger.LogWarning(exception, message, arguments);

  public static void Information(this ILogger logger, string message, params object?[] arguments) =>
    logger.LogInformation(message, arguments);

  public static void Information(
    this ILogger logger,
    Exception exception,
    string message,
    params object?[] arguments
  ) => logger.LogInformation(exception, message, arguments);

  public static void Error(this ILogger logger, string message, params object?[] arguments) =>
    logger.LogError(message, arguments);

  public static void Error(this ILogger logger, Exception exception, string message, params object?[] arguments) =>
    logger.LogError(exception, message, arguments);

  public static void Fatal(this ILogger logger, Exception exception, string message, params object?[] arguments) =>
    logger.LogCritical(exception, message, arguments);
}
