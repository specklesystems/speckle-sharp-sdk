namespace Speckle.Logging;

public interface ISpeckleLogger
{
  void Debug(string message, params object?[] arguments);
  void Debug(Exception exception, string message, params object?[] arguments);
  void Warning(string message, params object?[] arguments);
  void Warning(Exception exception, string message, params object?[] arguments);
  void Information(string message, params object?[] arguments);

  void Information(
    Exception exception,
    string message,
    params object?[] arguments
  );

  void Error(string message, params object?[] arguments);
  void Error(Exception exception, string message, params object?[] arguments);
  void Fatal(Exception exception, string message, params object?[] arguments);
}
