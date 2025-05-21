using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Speckle.Sdk.Models;

namespace Speckle.Sdk;

public record Application(string Name, string Slug);

public static class ServiceRegistration
{
  private static string GetAssemblyVersion() =>
    Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";

  public static IServiceCollection AddAutomateSdk(this IServiceCollection serviceCollection)
  {
    var executingAssembly = Assembly.GetExecutingAssembly().GetName();
    var speckleAssembly = typeof(Base).Assembly.GetName();
    AddAutomateSdk(
      serviceCollection,
      new SpeckleSdkOptions(
        new(executingAssembly.FullName, "automatefunction"),
        executingAssembly.Version?.ToString() ?? "Unknown",
        speckleAssembly.Version?.ToString(),
        [typeof(Base).Assembly, typeof().Assembly]
      )
    );

    return serviceCollection;
  }

  public static IServiceCollection AddAutomateSdk(
    this IServiceCollection serviceCollection,
    SpeckleSdkOptions speckleSdkOptions
  )
  {
    serviceCollection.AddSpeckleSdk(speckleSdkOptions);
  }

  public static IServiceCollection AddMatchingInterfacesAsTransient(
    this IServiceCollection serviceCollection,
    Assembly assembly,
    params Type[] classesToIgnore
  )
  {
    foreach (var type in assembly.ExportedTypes.Where(t => t.IsNonAbstractClass()))
    {
      if (classesToIgnore.Contains(type))
      {
        continue;
      }
      foreach (var matchingInterface in type.FindMatchingInterface())
      {
        serviceCollection.TryAddTransient(matchingInterface, type);
      }
    }

    return serviceCollection;
  }
}
