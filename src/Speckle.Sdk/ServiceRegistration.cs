using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Speckle.Sdk.Dependencies;
using Speckle.Sdk.Host;
using Speckle.Sdk.Logging;

namespace Speckle.Sdk;

public record SpeckleSdkOptions(
  string ApplicationName,
  string ApplicationSlug,
  string ApplicationVersion,
  string? SpeckleVersion,
  IEnumerable<Assembly>? Assemblies
);

public static class ServiceRegistration
{
  public static IServiceCollection AddSpeckleSdk(
    this IServiceCollection serviceCollection,
    SpeckleSdkOptions speckleSdkOptions
  )
  {
    var currentAssembly = Assembly.GetExecutingAssembly();
    var allAssembles = speckleSdkOptions.Assemblies?.ToList() ?? [];
    if (!allAssembles.Contains(currentAssembly))
    {
      allAssembles.Add(currentAssembly);
    }
    TypeLoader.Reset();
    TypeLoader.Initialize(allAssembles.ToArray());

    serviceCollection.AddLogging();

    serviceCollection.AddSingleton<ISpeckleApplication>(
      new SpeckleApplication
      {
        HostApplication = speckleSdkOptions.ApplicationName,
        HostApplicationVersion = speckleSdkOptions.ApplicationVersion,
        Slug = speckleSdkOptions.ApplicationSlug,
        SpeckleVersion = speckleSdkOptions.SpeckleVersion ?? GetAssemblyVersion(),
      }
    );
    serviceCollection.TryAddSingleton<ISdkActivityFactory, NullActivityFactory>();
    serviceCollection.TryAddSingleton<ISdkMetricsFactory, NullSdkMetricsFactory>();
    serviceCollection.AddMatchingInterfacesAsTransient(Assembly.GetExecutingAssembly());
    serviceCollection.AddMatchingInterfacesAsTransient(typeof(GraphQLRetry).Assembly);
    return serviceCollection;
  }

  private static string GetAssemblyVersion() =>
    Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";

  public static IServiceCollection AddSpeckleSdk(
    this IServiceCollection serviceCollection,
    string applicationName,
    string applicationSlug,
    string applicationVersion,
    string? speckleVersion = null,
    IEnumerable<Assembly>? assemblies = null
  ) =>
    serviceCollection.AddSpeckleSdk(
      new(applicationName, applicationSlug, applicationVersion, speckleVersion, assemblies)
    );

  public static IServiceCollection AddSpeckleSdk(
    this IServiceCollection serviceCollection,
    HostApplication application,
    HostAppVersion version,
    string speckleVersion
  ) =>
    serviceCollection.AddSpeckleSdk(
      application.Name,
      HostApplications.GetVersion(version),
      application.Slug,
      speckleVersion
    );

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
