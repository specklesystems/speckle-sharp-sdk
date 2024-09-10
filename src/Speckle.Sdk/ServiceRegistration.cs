using Microsoft.Extensions.DependencyInjection;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Host;
using Speckle.Sdk.Logging;

namespace Speckle.Sdk;

[GenerateAutoInterface]
public record SpeckleApplication(string Application, string Version, string Slug) : ISpeckleApplication
{
  public string ApplicationVersion => $"{Application} {Version}";
}

public static class ServiceRegistration
{
  public static IServiceCollection AddSpeckleSdk(this IServiceCollection serviceCollection, SpeckleConfiguration configuration)
  {
    serviceCollection.AddLogging();
    string application = configuration.Application.Name;
    foreach (var account in AccountManager.GetAccounts())
    {
      Analytics.AddConnectorToProfile(account.GetHashedEmail(), application);
      Analytics.IdentifyProfile(account.GetHashedEmail());
    }
    
    serviceCollection.AddSingleton<ISpeckleApplication>(new SpeckleApplication(application,
      HostApplications.GetVersion(configuration.Version),
      configuration.Application.Slug));

    return serviceCollection;
  }
}
