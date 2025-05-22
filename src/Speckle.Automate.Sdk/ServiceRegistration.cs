using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Speckle.Objects.Geometry;
using Speckle.Sdk;
using Speckle.Sdk.Models;

namespace Speckle.Automate.Sdk;

public static class ServiceRegistration
{
  
  /// <summary>
  /// Sets-up the serviceCollection with all the services in Speckle.Automate.Sdk and Speckle.Sdk 
  /// </summary>
  /// <param name="serviceCollection"></param>
  /// <returns></returns>
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
        [typeof(Base).Assembly, typeof(Point).Assembly]
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

    AddMatchingInterfacesAsTransient(
      serviceCollection,
      typeof(ServiceRegistration).Assembly,
      [typeof(AutomationContext)]
    );

    return serviceCollection;
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
