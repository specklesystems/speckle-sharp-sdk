using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Host;
using Speckle.Sdk.Logging;

namespace Speckle.Sdk;

[GenerateAutoInterface]
public class SpeckleApplication : ISpeckleApplication
{
  public string Application { get; init; }
  public string AppVersion { get; init; }
  public string Slug { get; init; }
  public string SpeckleVersion { get; init; }

  public string ApplicationAndVersion => $"{Application} {AppVersion}";
}

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
        Application = name,
        SpeckleVersion = speckleVersion,
        AppVersion = HostApplications.GetVersion(version),
        Slug = application.Slug
      }
    );
    serviceCollection.AddSingleton<ISdkActivityFactory, NullActivityFactory>();
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
