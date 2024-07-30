using Serilog;

namespace Speckle.Logging;

internal class SpeckleLogger : ISpeckleLogger
{
  private readonly Serilog.ILogger _logger;

  public SpeckleLogger(ILogger logger)
  {
    _logger = logger;
  }

  public void Debug(string message, params object?[] arguments) => _logger.Debug(message, arguments);

  public void Debug(Exception exception, string message, params object?[] arguments) =>
    _logger.Debug(exception, message, arguments);

  public void Warning(string message, params object?[] arguments) => _logger.Warning(message, arguments);

  public void Warning(Exception exception, string message, params object?[] arguments) =>
    _logger.Warning(exception, message, arguments);

  public void Information(string message, params object?[] arguments) => _logger.Information(message, arguments);

  public void Information(Exception exception, string message, params object?[] arguments) =>
    _logger.Information(exception, message, arguments);

  public void Error(string message, params object?[] arguments) => _logger.Error(message, arguments);

  public void Error(Exception exception, string message, params object?[] arguments) =>
    _logger.Error(exception, message, arguments);

  public void Fatal(Exception exception, string message, params object?[] arguments) =>
    _logger.Fatal(exception, message, arguments);
}
