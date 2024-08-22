using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Speckle.Sdk.Logging;

namespace Speckle.Sdk.Common;

public static class ServiceRegistration
{
  public static IServiceCollection AddSdk(this IServiceCollection serviceCollection)
  {
    serviceCollection.AddSingleton<ILoggerFactory>(new SpeckleLoggerFactory());
    serviceCollection.AddTransient(typeof(Logger<>), typeof(ILogger<>));
    return serviceCollection;
  }
}
