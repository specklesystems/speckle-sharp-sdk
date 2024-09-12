using Microsoft.Extensions.DependencyInjection;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Host;

namespace Speckle.Sdk;


[GenerateAutoInterface]
public record SpeckleApplication(string Application, string Version, string Slug) : ISpeckleApplication
{
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
      new SpeckleApplication(
        application,
        HostApplications.GetVersion(configuration.Version),
        configuration.Application.Slug
      )
    );

    return serviceCollection;
  }

}
