using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Speckle.Logging;

public static class SpeckleLogger
{
  private static ILoggerFactory _loggerFactory = new NullLoggerFactory();

  public static void Initialize(ILoggerFactory loggerFactory) => _loggerFactory = loggerFactory;

  public static ILogger Create<T>() => _loggerFactory.CreateLogger<T>();
  public static ILogger Create([CallerMemberName]string category = "SpeckleLogger") => _loggerFactory.CreateLogger(category);
  
}
