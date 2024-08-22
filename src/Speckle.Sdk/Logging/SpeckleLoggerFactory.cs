using Microsoft.Extensions.Logging;

namespace Speckle.Sdk.Logging;

public sealed class SpeckleLoggerFactory : ILoggerFactory
{
  public void Dispose() { }

  public ILogger CreateLogger(string categoryName) => new SpeckleLogger(SpeckleLog.Create(categoryName));

  public void AddProvider(ILoggerProvider provider) => throw new NotImplementedException();
}
