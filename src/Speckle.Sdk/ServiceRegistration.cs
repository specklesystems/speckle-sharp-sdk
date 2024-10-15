using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Speckle.Sdk.Host;
using Speckle.Sdk.Logging;

namespace Speckle.Sdk;

public static class ServiceRegistration
{
  public static IServiceCollection AddSpeckleSdk(
    this IServiceCollection serviceCollection,
    HostApplication application,
    HostAppVersion version,
    string speckleVersion
  )
  {
    serviceCollection.AddLogging();
    string name = application.Name;

    serviceCollection.AddSingleton<ISpeckleApplication>(
      new SpeckleApplication
      {
        HostApplication = name,
        SpeckleVersion = speckleVersion,
        HostApplicationVersion = HostApplications.GetVersion(version),
        Slug = application.Slug,
      }
    );
    serviceCollection.TryAddSingleton<ISdkActivityFactory, NullActivityFactory>();
    serviceCollection.TryAddSingleton<ISdkMetricsFactory, NullSdkMetricsFactory>();
    serviceCollection.AddMatchingInterfacesAsTransient(Assembly.GetExecutingAssembly());
    return serviceCollection;
  }

  public static IServiceCollection AddMatchingInterfacesAsTransient(
    this IServiceCollection serviceCollection,
    Assembly assembly
  )
  {
    foreach (var type in assembly.ExportedTypes.Where(t => t.IsNonAbstractClass()))
    {
      foreach (var matchingInterface in type.FindMatchingInterface())
      {
        serviceCollection.TryAddTransient(matchingInterface, type);
      }
    }

    return serviceCollection;
  }
}
