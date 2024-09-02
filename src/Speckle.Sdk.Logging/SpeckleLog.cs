using Serilog.Core;

namespace Speckle.Sdk.Logging;

public static class SpeckleLog
{
  public static Serilog.ILogger SpeckleLogger { get; set; } = Serilog.Core.Logger.None;
  public static ISpeckleLogger Logger => new SpeckleLogger(SpeckleLogger);

  public static ISpeckleLogger Create(string name) =>
    new SpeckleLogger(SpeckleLogger.ForContext(Constants.SourceContextPropertyName, name));
}
