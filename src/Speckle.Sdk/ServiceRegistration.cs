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

  public static async Task<IServiceProvider> UseSpeckleSdk(this IServiceProvider serviceProvider)
  {
    var accountManager = serviceProvider.GetRequiredService<IAccountManager>();
    var analytics = serviceProvider.GetRequiredService<IAnalytics>();
    var speckleApplication = serviceProvider.GetRequiredService<ISpeckleApplication>();

    foreach (var account in accountManager.GetAccounts())
    {
      await analytics
        .AddConnectorToProfile(account.GetHashedEmail(), speckleApplication.Application)
        .ConfigureAwait(false);
      await analytics.IdentifyProfile(account.GetHashedEmail()).ConfigureAwait(false);
    }

    return serviceProvider;
  }
}
