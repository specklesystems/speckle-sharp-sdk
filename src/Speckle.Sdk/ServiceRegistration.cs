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
  public string Version { get; init; }
  public string Slug { get; init; }

  public string ApplicationVersion => $"{Application} {Version}";
}

public static class ServiceRegistration
{
  public static IServiceCollection AddSpeckleSdk(
    this IServiceCollection serviceCollection,
    SpeckleConfiguration configuration
  )
  {
    serviceCollection.AddLogging();
    string application = configuration.Application.Name;

    serviceCollection.AddSingleton<ISpeckleApplication>(
      new SpeckleApplication
      {
        Application = application,
        Version = HostApplications.GetVersion(configuration.Version),
        Slug = configuration.Application.Slug
      }
    );
    serviceCollection.AddSingleton<ISdkActivityFactory, NullActivityFactory>();

    foreach (var type in Assembly.GetExecutingAssembly().ExportedTypes.Where(t => t.IsNonAbstractClass()))
    {
      foreach (var matchingInterface in type.FindMatchingInterface())
      {
        serviceCollection.TryAddTransient(matchingInterface, type);
      }
    }

    return serviceCollection;
  }
}
