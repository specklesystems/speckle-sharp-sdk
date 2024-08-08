using Serilog;
using Serilog.Core;

namespace Speckle.Sdk.Logging;

public static class SpeckleLog
{
  public static ISpeckleLogger Logger => new SpeckleLogger(Serilog.Log.Logger);

  public static ISpeckleLogger Create(string name) =>
    new SpeckleLogger(Log.Logger.ForContext(Constants.SourceContextPropertyName, name));
}
